/* DebtHelper — shared behavior (vanilla JS).
   Mobile nav: the hamburger toggles a .nav-open class on the header,
   which reveals the collapsed top-right + navy nav bar (see debthelper.css). */
(function () {
  var toggle = document.querySelector('.nav-toggle');
  var header = document.querySelector('.site-header');
  if (!toggle || !header) return;
  toggle.addEventListener('click', function () {
    var open = header.classList.toggle('nav-open');
    toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
  });
})();
