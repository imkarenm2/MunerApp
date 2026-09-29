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

// Evitar doble envío: los botones con data-cargando muestran un spinner al enviar
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;
    const boton = form.querySelector('button[type="submit"][data-cargando]');
    if (!boton) return;
    boton.disabled = true;
    boton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>' + boton.getAttribute('data-cargando');
});
