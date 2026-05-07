(function () {
    const errors = $("#campaign-errors");
    const success = $("#campaign-success");

    function showErrors(items) {
        success.addClass("d-none").empty();
        errors.removeClass("d-none").empty();
        $("<ul class='mb-0'></ul>")
            .append(items.map(item => $("<li></li>").text(item)))
            .appendTo(errors);
    }

    function showSuccess(message) {
        errors.addClass("d-none").empty();
        success.removeClass("d-none").text(message);
    }

    $(".campaign-upload-form").on("submit", function (event) {
        event.preventDefault();
        const form = $(this);
        if (form.data("busy")) {
            return;
        }
        form.data("busy", true);
        form.find(":submit").prop("disabled", true);
        const formData = new FormData(this);

        $.ajax({
            url: form.attr("action"),
            method: "POST",
            data: formData,
            processData: false,
            contentType: false,
            success: function () {
                showSuccess("Contacts uploaded.");
                window.setTimeout(() => window.location.reload(), 600);
            },
            error: function (xhr) {
                showErrors(xhr.responseJSON && xhr.responseJSON.errors ? xhr.responseJSON.errors : ["Upload failed."]);
                form.data("busy", false);
                form.find(":submit").prop("disabled", false);
            }
        });
    });

    $(".campaign-queue-form,.campaign-command-form").on("submit", function (event) {
        event.preventDefault();
        const form = $(this);
        const confirmMessage = form.data("confirm");
        if (confirmMessage && !window.confirm(confirmMessage)) {
            return;
        }
        if (form.data("busy")) {
            return;
        }
        form.data("busy", true);
        form.find(":submit").prop("disabled", true);

        $.ajax({
            url: form.attr("action"),
            method: "POST",
            data: form.serialize(),
            success: function () {
                showSuccess("Campaign updated.");
                window.setTimeout(() => window.location.reload(), 600);
            },
            error: function (xhr) {
                showErrors(xhr.responseJSON && xhr.responseJSON.errors ? xhr.responseJSON.errors : ["Campaign action failed."]);
                form.data("busy", false);
                form.find(":submit").prop("disabled", false);
            }
        });
    });
    const modal = new bootstrap.Modal(document.getElementById('queueStatusModal'));
    const modalBody = $("#modal-contacts-body");
    const retryBtn = $("#modal-retry-selected");
    const deleteBtn = $("#modal-delete-selected");
    const retryFailedBtn = $("#modal-retry-failed");
    let currentCampaignId = null;
    let allContacts = [];

    $(".queue-status-link").on("click", function () {
        currentCampaignId = $(this).data("id");
        modalBody.html("<tr><td colspan='4' class='text-center py-4'>Loading...</td></tr>");
        modal.show();

        $.get(`/Campaigns/GetContacts?campaignId=${currentCampaignId}`, function (contacts) {
            allContacts = contacts;
            renderContacts("all");
        });
    });

    function renderContacts(filter) {
        modalBody.empty();
        const filtered = allContacts.filter(c => {
            if (filter === "all") return true;
            const statuses = filter.split(",");
            return statuses.includes(c.statusText);
        });

        filtered.forEach(c => {
            const statusClass = c.statusText === "Delivered" || c.statusText === "Sent" ? "text-success" : (c.statusText === "Failed" ? "text-danger" : "text-secondary");
            
            modalBody.append(`
                <tr data-status="${c.statusText}">
                    <td><input type="checkbox" class="contact-checkbox" data-id="${c.id}" /></td>
                    <td class="font-monospace">${c.mobileNumber}</td>
                    <td><span class="badge bg-light ${statusClass} border">${c.statusText}</span></td>
                    <td class="small text-muted">${c.errorCode || "-"}</td>
                </tr>
            `);
        });
        
        if (filtered.length === 0) {
            modalBody.append("<tr><td colspan='4' class='text-center py-4 text-muted'>No contacts matching this filter.</td></tr>");
        }
        
        updateButtonStates();
    }

    $("[data-filter]").on("click", function () {
        $("[data-filter]").removeClass("active");
        $(this).addClass("active");
        renderContacts($(this).data("filter"));
    });

    $("#modal-select-all").on("change", function () {
        $(".contact-checkbox").prop("checked", $(this).prop("checked"));
        updateButtonStates();
    });

    modalBody.on("change", ".contact-checkbox", function () {
        updateButtonStates();
    });

    function updateButtonStates() {
        const selectedCount = $(".contact-checkbox:checked").length;
        retryBtn.prop("disabled", selectedCount === 0);
        deleteBtn.prop("disabled", selectedCount === 0);
    }
    
    retryFailedBtn.on("click", function () {
        if (!confirm(`Are you sure you want to retry all failed contacts for this campaign?`)) return;
        performModalAction('RetryFailed', { }, retryFailedBtn, "Retry Failed");
    });

    retryBtn.on("click", function () {
        const selectedIds = $(".contact-checkbox:checked").map(function() { return $(this).data("id"); }).get();
        if (selectedIds.length === 0) {
            alert("Please select at least one contact to retry.");
            return;
        }
        
        if (!confirm(`Are you sure you want to retry ${selectedIds.length} selected contacts?`)) return;
        
        performModalAction('RetrySelected', { contactIds: selectedIds }, retryBtn, "Retry Selected");
    });

    deleteBtn.on("click", function () {
        const selectedIds = $(".contact-checkbox:checked").map(function() { return $(this).data("id"); }).get();
        if (selectedIds.length === 0) {
            alert("Please select at least one contact to delete.");
            return;
        }
        
        if (!confirm(`This action is irreversible. Are you sure you want to delete ${selectedIds.length} selected contacts?`)) return;

        performModalAction('DeleteContacts', { contactIds: selectedIds }, deleteBtn, "Delete Selected");
    });

    function performModalAction(action, data, btn, btnText) {
        btn.prop("disabled", true).text("Processing...");
        const postData = Object.assign({ campaignId: currentCampaignId }, data);
        
        $.ajax({
            url: `/Campaigns/${action}`,
            method: 'POST',
            data: postData,
            headers: { 'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val() },
            success: function() { location.reload(); },
            error: function(xhr) {
                alert(`Action failed: ${extractAjaxErrors(xhr, "Unknown error").join(", ")}`);
                btn.prop("disabled", false).text(btnText);
            }
        });
    }

    // Handle AJAX errors
    $(document).ajaxError(function (event, xhr) {
        if (xhr.status === 401) {
            location.reload();
        }
    });
})();
