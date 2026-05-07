(function () {
    $("#select-all-reports").on("change", function () {
        $(".report-checkbox").prop("checked", $(this).prop("checked"));
    });

    $("#bulk-download-btn").on("click", function () {
        const selectedIds = $(".report-checkbox:checked").map(function () {
            return $(this).data("id");
        }).get();

        if (selectedIds.length === 0) {
            alert("Please select at least one campaign.");
            return;
        }

        const query = selectedIds.map(id => `ids=${id}`).join("&");
        window.location.href = `/Reports/ExportBulkCsv?${query}`;
    });
})();
