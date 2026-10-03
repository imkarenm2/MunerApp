// MunerApp · comportamientos generales de la interfaz

// Mostrar u ocultar contraseña: <button type="button" class="btn-ver-clave" data-ver-clave="IdDelInput">
document.addEventListener('click', function (e) {
    const boton = e.target.closest('[data-ver-clave]');
    if (!boton) return;
    const input = document.getElementById(boton.getAttribute('data-ver-clave'));
    if (!input) return;
    const mostrar = input.type === 'password';
    input.type = mostrar ? 'text' : 'password';
    boton.setAttribute('aria-label', mostrar ? 'Ocultar contraseña' : 'Mostrar contraseña');
    const icono = boton.querySelector('.bi');
    if (icono) {
        icono.classList.toggle('bi-eye', !mostrar);
        icono.classList.toggle('bi-eye-slash', mostrar);
    }
});

// Confirmar acciones importantes antes de enviarlas (observación de pruebas Sprint 1):
// <form data-confirmar="Mensaje" data-confirmar-titulo="¿Desactivar?" data-confirmar-boton="Sí, desactivar" data-confirmar-tipo="peligro">
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (!form.hasAttribute('data-confirmar') || form.dataset.confirmado === '1') return;
    const modalEl = document.getElementById('modalConfirmar');
    if (!modalEl || !window.bootstrap) return; // sin la ventana se envía normal

    e.preventDefault();
    e.stopImmediatePropagation();

    const peligro = form.getAttribute('data-confirmar-tipo') === 'peligro';
    modalEl.querySelector('[data-titulo]').textContent = form.getAttribute('data-confirmar-titulo') || '¿Estás seguro?';
    modalEl.querySelector('[data-mensaje]').textContent = form.getAttribute('data-confirmar');
    const icono = modalEl.querySelector('[data-icono]');
    icono.classList.toggle('peligro', peligro);
    icono.innerHTML = '<i class="bi ' + (peligro ? 'bi-exclamation-triangle' : 'bi-question-lg') + '"></i>';

    const aceptar = modalEl.querySelector('[data-aceptar]');
    aceptar.textContent = form.getAttribute('data-confirmar-boton') || 'Confirmar';
    aceptar.className = 'btn ' + (peligro ? 'btn-danger' : 'btn-primary');

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    aceptar.onclick = function () {
        aceptar.disabled = true;
        form.dataset.confirmado = '1';
        modal.hide();
        if (form.requestSubmit) form.requestSubmit(); else form.submit();
    };
    modalEl.addEventListener('hidden.bs.modal', function () { aceptar.disabled = false; }, { once: true });
    modal.show();
}, true);

// Evitar doble envío: los botones con data-cargando muestran un spinner al enviar
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;
    const boton = form.querySelector('button[type="submit"][data-cargando]');
    if (!boton) return;
    boton.disabled = true;
    boton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>' + boton.getAttribute('data-cargando');
});
