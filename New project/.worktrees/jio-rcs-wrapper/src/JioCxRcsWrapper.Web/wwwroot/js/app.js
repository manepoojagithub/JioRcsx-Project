(function () {
    const collapsedKey = "advait.sidebar.collapsed";

    function setSidebarCollapsed(collapsed) {
        $("body").toggleClass("sidebar-collapsed", collapsed);
        $("#sidebar-toggle").attr("aria-expanded", (!collapsed).toString());
        window.localStorage.setItem(collapsedKey, collapsed ? "1" : "0");
    }

    setSidebarCollapsed(window.localStorage.getItem(collapsedKey) === "1");

    $("#sidebar-toggle").on("click", function () {
        setSidebarCollapsed(!$("body").hasClass("sidebar-collapsed"));
    });

    function antiforgeryToken() {
        return $("input[name='__RequestVerificationToken']").first().val();
    }

    function showGlobalAlert(message, kind) {
        $("#global-alert")
            .removeClass("d-none alert-danger alert-success alert-warning")
            .addClass(`alert-${kind || "danger"}`)
            .text(message);
    }

    $.ajaxSetup({
        beforeSend: function (xhr) {
            const token = antiforgeryToken();
            if (token) {
                xhr.setRequestHeader("RequestVerificationToken", token);
            }
        },
        statusCode: {
            401: function () {
                window.location.href = "/Account/Login";
            },
            403: function () {
                showGlobalAlert("Not allowed", "danger");
            }
        }
    });

    function initializeGridFilters() {
        $(".panel-table").each(function () {
            const table = $(this);
            const header = table.find("thead tr").first();
            if (!header.length || table.data("filters-ready")) {
                return;
            }

            table.data("filters-ready", true);
            const filterRow = $("<tr class='grid-filter-row'></tr>");
            header.children("th").each(function (index) {
                const heading = $(this).text().trim();
                const isActionColumn = $(this).hasClass("action-cell") || heading.toLowerCase() === "actions";
                const cell = $("<th></th>");
                if (!isActionColumn) {
                    $("<input type='search' class='form-control form-control-sm grid-filter-input' />")
                        .attr("placeholder", `Filter ${heading}`)
                        .attr("aria-label", `Filter ${heading}`)
                        .data("column", index)
                        .appendTo(cell);
                }
                filterRow.append(cell);
            });
            header.after(filterRow);
        });
    }

    $(document).on("input", ".grid-filter-input", function () {
        const table = $(this).closest("table");
        const filters = table.find(".grid-filter-input").map(function () {
            return {
                column: $(this).data("column"),
                value: ($(this).val() || "").toString().trim().toLowerCase()
            };
        }).get().filter(filter => filter.value.length > 0);

        table.find("tbody tr").each(function () {
            const row = $(this);
            const isMatch = filters.every(filter => {
                const text = row.children("td").eq(filter.column).text().trim().toLowerCase();
                return text.includes(filter.value);
            });
            row.toggle(isMatch);
        });
    });

    initializeGridFilters();

    window.AdvaitPanel = {
        showGlobalAlert: showGlobalAlert
    };

    window.JioCxPanel = window.AdvaitPanel;
})();
