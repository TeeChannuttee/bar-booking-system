// Bar Booking System - Main JavaScript
$(document).ready(function () {
    // Initialize tooltips
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Initialize popovers
    var popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
    var popoverList = popoverTriggerList.map(function (popoverTriggerEl) {
        return new bootstrap.Popover(popoverTriggerEl);
    });

    // Auto-hide alerts after 5 seconds
    setTimeout(function () {
        $('.alert').not('.alert-permanent').fadeOut('slow');
    }, 5000);

    // Confirm delete actions
    $('.delete-confirm').on('click', function (e) {
        e.preventDefault();
        var url = $(this).attr('href');
        var title = $(this).data('title') || 'คุณแน่ใจหรือไม่?';
        var text = $(this).data('text') || 'การดำเนินการนี้ไม่สามารถยกเลิกได้!';

        Swal.fire({
            title: title,
            text: text,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#d33',
            cancelButtonColor: '#3085d6',
            confirmButtonText: 'ใช่, ดำเนินการ!',
            cancelButtonText: 'ยกเลิก'
        }).then((result) => {
            if (result.isConfirmed) {
                window.location.href = url;
            }
        });
    });

    // Table row clickable
    $('.clickable-row').click(function () {
        window.location = $(this).data('href');
    });

    // Format number inputs
    $('input[type="number"]').on('input', function () {
        var value = $(this).val();
        if (value < 0) $(this).val(0);
    });

    // Phone number format
    $('input[type="tel"]').on('input', function () {
        var value = $(this).val().replace(/\D/g, '');
        if (value.length > 10) value = value.substr(0, 10);

        if (value.length >= 6) {
            value = value.substr(0, 3) + '-' + value.substr(3, 3) + '-' + value.substr(6);
        } else if (value.length >= 3) {
            value = value.substr(0, 3) + '-' + value.substr(3);
        }

        $(this).val(value);
    });

    // Date validation - prevent past dates
    $('input[type="date"]').attr('min', function () {
        return new Date().toISOString().split('T')[0];
    });

    // Print function
    window.printElement = function (elem) {
        var printContents = document.getElementById(elem).innerHTML;
        var originalContents = document.body.innerHTML;
        document.body.innerHTML = printContents;
        window.print();
        document.body.innerHTML = originalContents;
    };

    // Copy to clipboard
    window.copyToClipboard = function (text) {
        navigator.clipboard.writeText(text).then(function () {
            Swal.fire({
                icon: 'success',
                title: 'คัดลอกแล้ว!',
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 2000
            });
        });
    };
});

// SignalR Connection for real-time updates
if (typeof signalR !== 'undefined') {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/bookingHub")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Start the connection
    async function start() {
        try {
            await connection.start();
            console.log("SignalR Connected.");
        } catch (err) {
            console.log(err);
            setTimeout(start, 5000);
        }
    }

    // Connection events
    connection.onclose(async () => {
        await start();
    });

    // Listen for booking updates
    connection.on("BookingStatusUpdated", function (bookingCode, status) {
        // Show notification
        Swal.fire({
            icon: 'info',
            title: 'อัปเดตสถานะ',
            text: `การจอง ${bookingCode} อัปเดตเป็น ${status}`,
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: 5000
        });

        // Update UI if on relevant page
        if ($('#booking-' + bookingCode).length) {
            location.reload();
        }
    });

    // Listen for new bookings (Admin only)
    connection.on("NewBooking", function (booking) {
        if ($('#adminDashboard').length) {
            // Play notification sound
            var audio = new Audio('/sounds/notification.mp3');
            audio.play();

            // Show notification
            Swal.fire({
                icon: 'success',
                title: 'การจองใหม่!',
                html: `<strong>${booking.customerName}</strong><br>
                       โต๊ะ ${booking.tableNumber}<br>
                       ${booking.date} ${booking.time}`,
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 5000
            });

            // Update dashboard
            updateDashboard();
        }
    });

    // Start SignalR connection
    start();
}

// Dashboard functions
function updateDashboard() {
    $.get('/Admin/GetDashboardData', function (data) {
        $('#todayBookings').text(data.todayBookings);
        $('#todayRevenue').text('฿' + data.todayRevenue.toLocaleString());
        $('#occupancyRate').text(data.occupancyRate + '%');
        // Update charts if needed
    });
}

// Booking functions
window.checkTableAvailability = function () {
    var branchId = $('input[name="BranchId"]:checked').val();
    var date = $('#BookingDate').val();
    var time = $('#StartTime').val();

    if (!branchId || !date || !time) {
        Swal.fire('ข้อมูลไม่ครบ', 'กรุณาเลือกสาขา วันที่ และเวลา', 'warning');
        return;
    }

    // Show loading
    Swal.fire({
        title: 'กำลังค้นหา...',
        allowOutsideClick: false,
        didOpen: () => {
            Swal.showLoading();
        }
    });

    // Load available tables
    $.get('/Booking/GetAvailableTables', {
        branchId: branchId,
        date: date,
        time: time,
        duration: $('#Duration').val(),
        guests: $('#NumberOfGuests').val()
    }, function (tables) {
        Swal.close();

        if (tables.length === 0) {
            Swal.fire('ไม่มีโต๊ะว่าง', 'ไม่มีโต๊ะว่างในช่วงเวลาที่เลือก', 'info');
        } else {
            // Show tables
            displayAvailableTables(tables);
        }
    });
};

// Dark mode toggle
if (localStorage.getItem('darkMode') === 'true') {
    document.body.classList.add('dark-mode');
}

window.toggleDarkMode = function () {
    document.body.classList.toggle('dark-mode');
    localStorage.setItem('darkMode', document.body.classList.contains('dark-mode'));
};
