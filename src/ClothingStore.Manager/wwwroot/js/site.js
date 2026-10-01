// <form data-confirm="Delete X?"> asks before submitting (text stays out of inline JS).
document.addEventListener('submit', function (e) {
    var message = e.target.getAttribute && e.target.getAttribute('data-confirm');
    if (message && !window.confirm(message)) {
        e.preventDefault();
    }
});
