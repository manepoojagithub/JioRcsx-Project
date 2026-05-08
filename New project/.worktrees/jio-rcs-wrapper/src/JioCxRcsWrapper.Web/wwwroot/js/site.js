// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Global double-click prevention
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (form.getAttribute('data-prevent-double-click') === 'false') {
        return;
    }
    
    const submitButtons = form.querySelectorAll('button[type="submit"], input[type="submit"]');
    submitButtons.forEach(button => {
        // We use setTimeout to allow the form to actually submit before disabling
        // If we disable immediately, some browsers might not submit the form
        setTimeout(() => {
            button.disabled = true;
            const originalText = button.innerHTML || button.value;
            if (button.tagName === 'BUTTON') {
                button.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Processing...';
            } else {
                button.value = 'Processing...';
            }
        }, 0);
    });
});

// Global grid filtering
document.addEventListener('keypress', function (e) {
    if (e.target.classList.contains('grid-filter') && e.key === 'Enter') {
        applyGridFilters();
    }
});

document.addEventListener('change', function (e) {
    if (e.target.classList.contains('grid-filter-select')) {
        applyGridFilters();
    }
});

function applyGridFilters() {
    const filterInputs = document.querySelectorAll('.grid-filter, .grid-filter-select');
    const url = new URL(window.location.href);
    filterInputs.forEach(input => {
        const name = input.getAttribute('name') || input.getAttribute('data-column');
        if (input.value) {
            url.searchParams.set(name, input.value);
        } else {
            url.searchParams.delete(name);
        }
    });
    url.searchParams.set('pageNumber', '1');
    window.location.href = url.toString();
}

function extractAjaxErrors(xhr, fallbackMessage) {
    if (xhr.responseJSON && xhr.responseJSON.errors) {
        return xhr.responseJSON.errors;
    }

    if (xhr.status === 401) {
        return ["Your session expired. Please login again."];
    }

    if (xhr.status === 403) {
        return ["Not allowed. Please check permissions for your role."];
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
