/* Unconquered by Debt — page behavior (vanilla JS). */
(function () {
  // Footer year.
  var y = document.getElementById('year');
  if (y) y.textContent = new Date().getFullYear();

  // Contact form: client-side validation only for now.
  // NOTE (developer): wire the submit to a backend/CRM (e.g. /api/ubd-contact) before going live.
  var form = document.getElementById('ubdForm');
  if (!form) return;
  var success = document.getElementById('success');

  function fieldOf(el) { return el.closest('.field'); }
  function isEmail(v) { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v); }

  form.addEventListener('submit', function (e) {
    e.preventDefault();

    // Honeypot: a filled hidden field means bot — pretend success, do nothing.
    var hp = document.getElementById('website');
    if (hp && hp.value) { form.style.display = 'none'; success.classList.add('show'); return; }

    var ok = true;

    ['name', 'role'].forEach(function (id) {
      var el = document.getElementById(id);
      var bad = !el.value.trim();
      fieldOf(el).classList.toggle('invalid', bad);
      if (bad) ok = false;
    });

    var email = document.getElementById('email');
    var emailBad = !isEmail(email.value.trim());
    fieldOf(email).classList.toggle('invalid', emailBad);
    if (emailBad) ok = false;

    if (!ok) {
      var firstBad = form.querySelector('.field.invalid');
      if (firstBad) firstBad.scrollIntoView({ behavior: 'smooth', block: 'center' });
      return;
    }

    // TODO (developer): POST the collected fields to your intake API here.
    form.style.display = 'none';
    success.classList.add('show');
    success.scrollIntoView({ behavior: 'smooth', block: 'center' });
  });

  // Clear the invalid state as the visitor fixes a field.
  form.addEventListener('input', function (e) {
    var f = e.target.closest('.field');
    if (f) f.classList.remove('invalid');
  });
})();
