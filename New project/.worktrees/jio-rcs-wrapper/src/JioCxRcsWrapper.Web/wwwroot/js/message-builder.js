(function () {
    const form = $("#message-builder-form");
    const preview = $("#payload-preview");
    const errors = $("#message-builder-errors");
    const ctaList = $("#cta-list");

    function setType(type) {
        $("#MessageType").val(type);
        $("[data-panel]").addClass("d-none");
        $(`[data-panel='${type}']`).removeClass("d-none");
        $("[data-message-type]").each(function () {
            const button = $(this);
            const active = button.data("message-type") === type;
            button.toggleClass("btn-primary", active);
            button.toggleClass("btn-outline-primary", !active);
        });
    }

    function updateCounts() {
        $("[data-count-for]").each(function () {
            const target = $(this).data("count-for");
            const value = $(`#${target}`).val() || "";
            $(this).text(value.length);
        });
    }

    function syncRichEditors() {
        $(".rich-editor").each(function () {
            const target = $(this).data("hidden-target");
            $(`#${target}`).val($(this).html().trim());
        });
    }

    function hydrateRichEditors() {
        $(".rich-editor").each(function () {
            const target = $(this).data("hidden-target");
            $(this).html($(`#${target}`).val() || "");
        });
    }

    function showErrors(items) {
        errors.removeClass("d-none").empty();
        $("<ul class='mb-0'></ul>")
            .append(items.map(item => $("<li></li>").text(item)))
            .appendTo(errors);
    }

    function showSuccess(message) {
        errors.removeClass("d-none alert-danger").addClass("alert-success").text(message);
    }

    function extractAjaxErrors(xhr, fallbackMessage) {
        if (xhr.responseJSON && xhr.responseJSON.errors) {
            return xhr.responseJSON.errors;
        }

        if (xhr.status === 401) {
            return ["Your session expired. Please login again, then save the template."];
        }

        if (xhr.status === 403) {
            return ["Not allowed. Please check Message Builder Add permission for your role."];
        }

        if (xhr.status === 413) {
            return ["The selected file is too large for the server upload limit."];
        }

        const responseText = (xhr.responseText || "")
            .replace(/<script[\s\S]*?<\/script>/gi, " ")
            .replace(/<style[\s\S]*?<\/style>/gi, " ")
            .replace(/<[^>]+>/g, " ")
            .replace(/\s+/g, " ")
            .trim();
        const detail = responseText ? ` ${responseText.slice(0, 300)}` : "";
        const status = xhr.status ? ` HTTP ${xhr.status}` : " HTTP 0";
        const statusText = xhr.statusText ? ` ${xhr.statusText}` : "";

        return [`${fallbackMessage}${status}${statusText}.${detail}`.trim()];
    }

    function clearTransientErrors() {
        if (errors.hasClass("alert-danger")) {
            errors.addClass("d-none").empty();
        }
    }

    function refreshCtaIndexes() {
        ctaList.find("[data-cta-row]").each(function (index) {
            $(this).find("strong").text(`CTA ${index + 1}`);
            $(this).find("input,select").each(function () {
                const field = this.name.split(".").pop();
                this.name = `Ctas[${index}].${field}`;
                this.id = `Ctas_${index}__${field}`;
                $(this).closest(".col-md-6").find("label").attr("for", this.id);
            });
        });
        $("#add-cta").prop("disabled", ctaList.find("[data-cta-row]").length >= 4);
    }

    function updateVisualPreview() {
        $("#preview-title").html($("[data-hidden-target='Title']").html() || "");
        $("#preview-description").html($("[data-hidden-target='Description']").html() || "");
        $("#preview-footer").html($("[data-hidden-target='Footer']").html() || "");
        $("#preview-plain").html($("[data-hidden-target='Text']").html() || "");

        const file = $("#MediaFile")[0] && $("#MediaFile")[0].files[0];
        const localMediaUrl = $("#LocalMediaPath").val() || "";
        const fallbackMediaUrl = $("#MediaUrl").val() || "";
        const mediaUrl = file ? URL.createObjectURL(file) : (localMediaUrl || fallbackMediaUrl);
        const media = $("#preview-media").empty();
        if (!mediaUrl) {
            return;
        }

        if (file && file.type.startsWith("video/")) {
            $("<video controls class='w-100 rounded'></video>").attr("src", mediaUrl).appendTo(media);
        } else {
            $("<img class='img-fluid rounded border' alt='Preview media' />")
                .attr("src", mediaUrl)
                .on("error", function () {
                    if (!file && fallbackMediaUrl && this.src !== fallbackMediaUrl) {
                        this.src = fallbackMediaUrl;
                    }
                })
                .appendTo(media);
        }
    }

    $("[data-message-type]").on("click", function () {
        setType($(this).data("message-type"));
    });

    $("[data-rich-command]").on("click", function () {
        document.execCommand($(this).data("rich-command"), false, null);
        syncRichEditors();
        updateVisualPreview();
    });

    $("#add-cta").on("click", function () {
        const index = ctaList.find("[data-cta-row]").length;
        if (index >= 4) {
            return;
        }

        $.get(`/MessageBuilder/CtaRow?index=${index}`, function (html) {
            ctaList.append(html);
            refreshCtaIndexes();
        });
    });

    ctaList.on("click", "[data-remove-cta]", function () {
        $(this).closest("[data-cta-row]").remove();
        refreshCtaIndexes();
    });

    form.on("input change", function () {
        syncRichEditors();
        updateCounts();
        updateVisualPreview();
        clearTransientErrors();
    });

    form.on("click", "#preview-payload-btn", function (event) {
        event.preventDefault();
        const btn = $(this);
        if (btn.data("busy")) return;
        
        btn.data("busy", true).text("Processing...");
        syncRichEditors();
        errors.addClass("d-none").empty();
        errors.removeClass("alert-success").addClass("alert-danger");
        preview.text("");

        $.ajax({
            url: form.attr("action"),
            method: "POST",
            data: new FormData(form[0]),
            processData: false,
            contentType: false,
            success: function (response) {
                const parsed = JSON.parse(response.payloadJson);
                preview.text(JSON.stringify(parsed, null, 2));
                btn.data("busy", false).text("Preview Payload");
            },
            error: function (xhr) {
                showErrors(extractAjaxErrors(xhr, "Unable to build payload."));
                btn.data("busy", false).text("Preview Payload");
            }
        });
    });

    $("#save-template").on("click", function () {
        const button = $(this);
        if (button.data("busy")) {
            return;
        }

        button.data("busy", true).prop("disabled", true);
        syncRichEditors();
        errors.addClass("d-none").empty();
        errors.removeClass("alert-success").addClass("alert-danger");

        const formData = new FormData(form[0]);
        $.ajax({
            url: form.data("save-url") || "/MessageBuilder/SaveTemplate",
            method: "POST",
            data: formData,
            processData: false,
            contentType: false,
            success: function () {
                showSuccess("Template saved.");
                button.data("busy", false).prop("disabled", false);
            },
            error: function (xhr) {
                showErrors(extractAjaxErrors(xhr, "Unable to save template."));
                button.data("busy", false).prop("disabled", false);
            }
        });
    });

    $("#clear-builder").on("click", function () {
        form[0].reset();
        ctaList.empty();
        $(".rich-editor").empty();
        preview.text("");
        errors.addClass("d-none").empty();
        setType("PlainText");
        updateCounts();
        refreshCtaIndexes();
    });

    hydrateRichEditors();
    setType($("#MessageType").val() || "PlainText");
    syncRichEditors();
    updateCounts();
    updateVisualPreview();
    refreshCtaIndexes();
})();
