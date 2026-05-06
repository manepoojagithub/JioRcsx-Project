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
})();
