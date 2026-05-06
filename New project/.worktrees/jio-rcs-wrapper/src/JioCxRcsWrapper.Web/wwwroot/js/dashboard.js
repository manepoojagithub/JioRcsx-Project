(function () {
    let deliveryChart;
    let messageChart;

    const palette = {
        purple: "#5b46f6",
        teal: "#23b7b7",
        green: "#12a150",
        amber: "#d97706",
        red: "#dc2626",
        ink: "#16181d",
        muted: "#8b909a",
        grid: "rgba(20, 24, 33, .08)"
    };

    function setCardValues(data) {
        Object.keys(data).forEach(key => {
            $(`[data-dashboard-value='${key}']`).text(data[key]);
        });
    }

    function chartData(points, labelKey, valueKey) {
        const statusColors = {
            Pending: "#9ca3af",
            Sent: palette.purple,
            Delivered: palette.green,
            Failed: palette.red,
            Opened: palette.teal,
            Clicked: palette.amber
        };

        return {
            labels: points.map(point => point[labelKey]),
            datasets: [{
                data: points.map(point => point[valueKey]),
                backgroundColor: points.map(point => statusColors[point[labelKey]] || palette.purple),
                borderWidth: 0,
                borderRadius: 8
            }]
        };
    }

    function baseChartOptions(showLegend) {
        return {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: showLegend,
                    position: "bottom",
                    labels: { usePointStyle: true, boxWidth: 8, color: palette.muted }
                }
            },
            scales: {
                x: { grid: { display: false }, ticks: { color: palette.muted } },
                y: { beginAtZero: true, grid: { color: palette.grid }, ticks: { color: palette.muted, precision: 0 } }
            }
        };
    }

    function deliveryPoints(data) {
        return [
            { label: "Delivered", value: data.delivered },
            { label: "Failed", value: data.failed },
            { label: "Pending", value: data.pending },
            { label: "Opened", value: data.opened },
            { label: "Clicked", value: data.clicked }
        ];
    }

    function messageStatusPoints(data) {
        return [
            { label: "Pending", value: data.pending },
            { label: "Sent", value: data.sent },
            { label: "Delivered", value: data.delivered },
            { label: "Failed", value: data.failed },
            { label: "Opened", value: data.opened },
            { label: "Clicked", value: data.clicked }
        ];
    }

    function upsertCharts(data) {
        const delivery = deliveryPoints(data);
        const messages = messageStatusPoints(data);

        if (!deliveryChart) {
            deliveryChart = new Chart($("#deliveryChart"), {
                type: "doughnut",
                data: chartData(delivery, "label", "value"),
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    cutout: "68%",
                    plugins: baseChartOptions(true).plugins
                }
            });

            messageChart = new Chart($("#messageChart"), {
                type: "bar",
                data: chartData(messages, "label", "value"),
                options: baseChartOptions(false)
            });

            return;
        }

        deliveryChart.data = chartData(delivery, "label", "value");
        messageChart.data = chartData(messages, "label", "value");
        deliveryChart.update();
        messageChart.update();
    }

    function statusClass(status) {
        const normalized = (status || "").toLowerCase();
        if (normalized.includes("queued") || normalized.includes("schedule")) return "status-scheduled";
        if (normalized.includes("fail") || normalized.includes("pause")) return "status-failed";
        if (normalized.includes("draft")) return "status-draft";
        return "status-active";
    }

    function renderRecent(data) {
        const tbody = $("#recent-campaigns").empty();
        if (!data.recentCampaigns.length) {
            $("<tr></tr>")
                .append($("<td colspan='4' class='text-muted py-4 text-center'></td>").text("No campaign activity yet."))
                .appendTo(tbody);
            return;
        }

        data.recentCampaigns.forEach(item => {
            $("<tr></tr>")
                .append($("<td class='fw-semibold'></td>").text(item.campaign))
                .append($("<td></td>").append($("<span></span>").addClass(`dashboard-status ${statusClass(item.status)}`).text(item.status)))
                .append($("<td></td>").text(item.contacts))
                .append($("<td class='text-muted'></td>").text(new Date(item.createdAt).toLocaleString()))
                .appendTo(tbody);
        });
    }

    function renderInsights(data) {
        const insights = [];
        if (data.failed > 0) {
            insights.push(["Failure review", `${data.failed} contacts failed. Check reports for API responses.`]);
        } else {
            insights.push(["Delivery clean", "No failed contacts in the current dashboard scope."]);
        }

        if (data.pending > 0) {
            insights.push(["Queue watch", `${data.pending} contacts are still pending or waiting for processing.`]);
        } else {
            insights.push(["Queue clear", "No pending contacts are visible right now."]);
        }

        const container = $("#dashboard-insights").empty();
        insights.forEach(([title, body]) => {
            $("<div class='dashboard-insight'></div>")
                .append($("<span class='dashboard-insight-icon'></span>").text("i"))
                .append($("<div></div>").append($("<strong></strong>").text(title)).append($("<p></p>").text(body)))
                .appendTo(container);
        });
    }

    function render(data) {
        setCardValues(data);
        if (window.Chart) {
            upsertCharts(data);
        }
        renderRecent(data);
        renderInsights(data);
    }

    function refresh() {
        $.getJSON("/Dashboard/Data", render);
    }

    render(window.dashboardInitialData);

    if (window.signalR) {
        const connection = new signalR.HubConnectionBuilder().withUrl("/hubs/dashboard").withAutomaticReconnect().build();
        connection.on("dashboardUpdated", refresh);
        connection.start().catch(() => { });
    }
})();
