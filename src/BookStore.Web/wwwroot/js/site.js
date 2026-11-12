// Nút +/- cho ô số lượng (trang chi tiết sách và giỏ hàng)
document.querySelectorAll('.qty-input [data-step]').forEach(btn => {
    btn.addEventListener('click', () => {
        const input = btn.parentElement.querySelector('input[type=number]');
        const min = parseInt(input.min || '0', 10);
        const max = parseInt(input.max || '999', 10);
        const value = Math.min(max, Math.max(min, (parseInt(input.value, 10) || 0) + parseInt(btn.dataset.step, 10)));
        input.value = value;
        if (btn.hasAttribute('data-autosubmit')) btn.closest('form').submit();
    });
});

// Tự ẩn thông báo toast sau 4 giây
document.querySelectorAll('.toast.show').forEach(el => {
    setTimeout(() => bootstrap.Toast.getOrCreateInstance(el).hide(), 4000);
});
