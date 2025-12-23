(function($) {
    /* "use strict" */


    var dzChartlist = function() {

        var screenWidth = $(window).width();


        var barChart = function() {
            // Bar Chart
            if (jQuery('#barChart_2').length > 0) {
                const barChart_2 = document.getElementById("barChart_2").getContext('2d');
                //generate gradient
                const barChart_2gradientStroke = barChart_2.createLinearGradient(0, 0, 0, 250);
                barChart_2gradientStroke.addColorStop(0, "rgba(54, 149, 235, 1)");
                barChart_2gradientStroke.addColorStop(1, "rgba(54, 149, 235, 1)");

                barChart_2.height = 100;

                new Chart(barChart_2, {
                    type: 'bar',
                    data: {
                        defaultFontFamily: 'Poppins',
                        labels: ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul"],
                        datasets: [{
                            label: "My First dataset",
                            data: [65, 59, 80, 81, 56, 55, 40],
                            borderColor: barChart_2gradientStroke,
                            borderWidth: "0",
                            backgroundColor: barChart_2gradientStroke,
                            hoverBackgroundColor: barChart_2gradientStroke
                        }]
                    },
                    options: {
                        legend: false,
                        scales: {
                            yAxes: [{
                                ticks: {
                                    beginAtZero: true
                                }
                            }],
                            xAxes: [{
                                // Change here
                                barPercentage: 0.5
                            }]
                        }
                    }
                });
            }
        }
        var pieChart = function() {
            //Pie chart with custom labels
            var data = {
                labels: ['America', 'Russia', 'China'],
                series: [20, 15, 40]
            };

            var options = {
                labelInterpolationFnc: function(value) {
                    return value[0]
                }
            };

            var responsiveOptions = [
                ['screen and (min-width: 640px)', {
                    chartPadding: 30,
                    labelOffset: 100,
                    labelDirection: 'explode',
                    labelInterpolationFnc: function(value) {
                        return value;
                    }
                }],
                ['screen and (min-width: 1024px)', {
                    labelOffset: 80,
                    chartPadding: 20
                }]
            ];

            new Chartist.Pie('#pie-chart', data, options, responsiveOptions);


        }
        var gdpData = {
            "us": 34.56,
            "in": 34.56,
            "gb": 34.56,
            "tr": 34.56,
            "ru": 34.56,
        }
        var worldMap = function() {
            $("#world-map").vectorMap({
                map: "world_en",
                backgroundColor: "transparent",
                borderColor: "#8c8c8c",
                color: "#F7F8FA",
                colors: { in: "#3695eb",
                    gb: "#3695eb",
                    tr: "#3695eb",
                    us: "#3695eb",
                    ru: "#3695eb",
                },
                onLabelShow: function(event, label, code) {
                    label.text(label.text() + " (" + gdpData[code] + ")");
                },
                enableZoom: true,
                showTooltip: true,
                selectedColor: "#3695eb",
                hoverColor: "#1e78ff",
            });
        }

        /* Function ============ */
        return {
            init: function() {

            },


            load: function() {
                barChart();
                pieChart();
                worldMap();
            },

            resize: function() {}
        }

    }();

    jQuery(document).ready(function() {});

    jQuery(window).on('load', function() {
        dzChartlist.load();
    });

    jQuery(window).on('resize', function() {


    });

})(jQuery);