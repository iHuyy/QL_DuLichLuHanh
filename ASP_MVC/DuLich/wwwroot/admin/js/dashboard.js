$(document).ready(function () {
    loadDashboardData();

    // Refresh data every 5 minutes
    setInterval(loadDashboardData, 300000);
});

function loadDashboardData() {
    // Load summary statistics
    $.ajax({
        url: '/admin/api/SystemStats',
        method: 'GET',
        success: function (data) {
            updateSummaryStats(data);
        },
        error: function (xhr, status, error) {
            console.error('Error loading dashboard stats:', error);
        }
    });

    // Load monthly bookings data for chart
    $.ajax({
        url: '/admin/api/MonthlyBookings',
        method: 'GET',
        success: function (data) {
            updateMonthlyBookingsChart(data);
        },
        error: function (xhr, status, error) {
            console.error('Error loading monthly bookings:', error);
        }
    });

    // Load tour type distribution
    $.ajax({
        url: '/admin/api/TourTypes',
        method: 'GET',
        success: function (data) {
            updateTourTypeDistribution(data);
        },
        error: function (xhr, status, error) {
            console.error('Error loading tour types:', error);
        }
    });

    // Load popular tours
    $.ajax({
        url: '/admin/api/PopularTours',
        method: 'GET',
        success: function (data) {
            updatePopularToursTable(data);
        },
        error: function (xhr, status, error) {
            console.error('Error loading popular tours:', error);
        }
    });
}

function updateSummaryStats(data) {
    $('#totalTours').text(data.totalTours.toLocaleString('vi-VN') || 0);
    $('#totalUsers').text(data.totalUsers.toLocaleString('vi-VN') || 0);
    $('#todayBookings').text(data.todayBookings.toLocaleString('vi-VN') || 0);
    $('#totalRevenue').text(formatCurrency(data.totalRevenue || 0));
    $('#activeTours').text(data.activeTours.toLocaleString('vi-VN') || 0);
    $('#expiredTours').text(data.expiredTours.toLocaleString('vi-VN') || 0);
}

function updateMonthlyBookingsChart(data) {
    const ctx = document.getElementById('monthlyBookingsChart').getContext('2d');

    if (window.monthlyBookingsChart) {
        window.monthlyBookingsChart.destroy();
    }

    window.monthlyBookingsChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: data.labels,
            datasets: [{
                label: 'Số lượng đặt tour',
                data: data.values,
                backgroundColor: 'rgba(54, 162, 235, 0.5)',
                borderColor: 'rgba(54, 162, 235, 1)',
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        stepSize: 1
                    }
                }
            }
        }
    });
}

function updateTourTypeDistribution(data) {
    const chartData = {
        labels: data.labels,
        series: data.values
    };

    const options = {
        donut: true,
        donutWidth: 60,
        startAngle: 270,
        total: 200
    };

    if (window.tourTypeChart) {
        window.tourTypeChart.destroy();
    }

    window.tourTypeChart = new Chartist.Pie('#tourTypeDistribution', chartData, options);
}

function updatePopularToursTable(data) {
    const tbody = $('#popularToursTable tbody');
    tbody.empty();

    data.forEach(tour => {
        tbody.append(`
            <tr>
                <td>${tour.tenTour}</td>
                <td>${tour.soLuotDat}</td>
                <td>
                    <div class="rating">
                        ${generateStarRating(tour.danhGia)}
                    </div>
                </td>
                <td>${formatCurrency(tour.doanhThu)}</td>
            </tr>
        `);
    });
}

function generateStarRating(rating) {
    const fullStars = Math.floor(rating);
    const hasHalfStar = rating % 1 >= 0.5;
    let stars = '';

    for (let i = 0; i < 5; i++) {
        if (i < fullStars) {
            stars += '<i class="fa fa-star text-warning"></i>';
        } else if (i === fullStars && hasHalfStar) {
            stars += '<i class="fa fa-star-half-alt text-warning"></i>';
        } else {
            stars += '<i class="far fa-star text-warning"></i>';
        }
    }

    return stars;
}

function formatCurrency(amount) {
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND'
    }).format(amount);
}