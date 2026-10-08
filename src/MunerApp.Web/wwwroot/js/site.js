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
    // Si el formulario tiene errores, primero se muestran los errores (no la ventana)
    if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;
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

// ===================== Sprint 2 =====================

// Copiar al portapapeles: <button data-copiar="idDelElemento">
document.addEventListener('click', function (e) {
    const boton = e.target.closest('[data-copiar]');
    if (!boton) return;
    const origen = document.getElementById(boton.getAttribute('data-copiar'));
    if (!origen || !navigator.clipboard) return;
    navigator.clipboard.writeText(origen.textContent.trim()).then(function () {
        const texto = boton.querySelector('span');
        if (!texto) return;
        const original = texto.textContent;
        texto.textContent = '¡Copiado!';
        setTimeout(function () { texto.textContent = original; }, 1800);
    });
});

// Archivos: mostrar el nombre elegido y la vista previa del logo
document.addEventListener('change', function (e) {
    const input = e.target;
    if (!(input instanceof HTMLInputElement) || input.type !== 'file') return;

    const formAuto = input.closest('form[data-subida-automatica]');

    // Revisar el tamaño antes de enviar (máximo 5 MB), para no esperar una subida que el servidor va a rechazar
    const MAX_MB = 5;
    const elegido = input.files[0];
    const muyPesado = Array.from(input.files).find(function (f) { return f.size > MAX_MB * 1024 * 1024; });
    if (muyPesado) {
        const mb = (muyPesado.size / 1024 / 1024).toFixed(1).replace('.', ',');
        input.value = '';
        const zonaError = input.parentElement.querySelector('[data-nombre-archivo]');
        if (zonaError) {
            zonaError.textContent = 'El archivo pesa ' + mb + ' MB. El máximo es ' + MAX_MB + ' MB: elige otro o redúcelo.';
            zonaError.classList.add('text-danger');
        }
        return;
    }

    // Galería: subir apenas se elige la foto
    if (formAuto && elegido) {
        const etiqueta = formAuto.querySelector('label');
        if (etiqueta) etiqueta.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Subiendo...';
        formAuto.submit();
        return;
    }

    if (!input.hasAttribute('data-archivo')) return;
    const contenedor = input.parentElement;
    const nombre = contenedor.querySelector('[data-nombre-archivo]');
    if (nombre) nombre.classList.remove('text-danger');
    const zona = contenedor.querySelector('.zona-archivo');
    const archivo = input.files[0];
    // Con varios archivos (fotos de la historia clínica) se muestra la cantidad
    const varios = input.multiple && input.files.length > 1;
    if (nombre) nombre.textContent = varios ? input.files.length + ' fotos seleccionadas'
        : (archivo ? archivo.name : (input.multiple ? 'Selecciona hasta 5 fotos' : 'Selecciona el archivo'));
    if (zona) zona.classList.toggle('con-archivo', !!archivo);

    if (archivo && input.getAttribute('data-vista-previa') === 'logo' && archivo.type.startsWith('image/')) {
        const vista = document.querySelector('[data-vista-previa-logo]');
        if (vista) {
            const img = document.createElement('img');
            img.className = 'logo-esal';
            img.alt = 'Vista previa del logo';
            img.style.width = img.style.height = '88px';
            img.src = URL.createObjectURL(archivo);
            vista.replaceChildren(img);
        }
    }
});

// Valor en pesos: 50000 → 50.000 mientras se escribe
document.addEventListener('input', function (e) {
    const input = e.target;
    if (!(input instanceof HTMLInputElement) || !input.hasAttribute('data-moneda')) return;
    const digitos = input.value.replace(/\D/g, '').replace(/^0+/, '').slice(0, 12);
    input.value = digitos.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
});

// Postulación: mostrar los datos académicos solo para practicantes
document.addEventListener('change', function (e) {
    const radio = e.target;
    if (!(radio instanceof HTMLInputElement) || !radio.hasAttribute('data-tipo-postulacion')) return;
    const bloque = document.querySelector('[data-bloque-practicante]');
    if (bloque) bloque.classList.toggle('d-none', radio.value !== 'PracticanteSalud');
});

// Galería del perfil: ver la foto en grande
document.addEventListener('click', function (e) {
    const item = e.target.closest('[data-galeria]');
    const visor = document.getElementById('visorFoto');
    if (!item || !visor || !window.bootstrap) return;
    visor.querySelector('[data-visor-img]').src = item.getAttribute('data-galeria');
    bootstrap.Modal.getOrCreateInstance(visor).show();
});

// Contador de caracteres: <textarea maxlength="500" data-contador>
document.querySelectorAll('[data-contador][maxlength]').forEach(function (campo) {
    const contador = document.createElement('div');
    contador.className = 'form-text text-end';
    contador.setAttribute('aria-live', 'polite');
    const actualizar = function () { contador.textContent = campo.value.length + ' / ' + campo.getAttribute('maxlength'); };
    campo.insertAdjacentElement('afterend', contador);
    campo.addEventListener('input', actualizar);
    actualizar();
});

// Datos para donar: el campo de número cambia según el tipo (observación 7 de pruebas del Sprint 2)
(function () {
    const tipoCuenta = document.querySelector('[data-tipo-cuenta]');
    const numero = document.querySelector('[data-numero-cuenta]');
    if (!tipoCuenta || !numero) return;
    const tipoLlave = document.querySelector('[data-tipo-llave]');
    const bloqueLlave = document.querySelector('[data-bloque-llave]');
    const etiqueta = document.querySelector('[data-etiqueta-numero]');
    const ayuda = document.querySelector('[data-ayuda-numero]');
    const nit = numero.getAttribute('data-nit-esal') || '';

    const reglas = {
        Ahorros: { etiqueta: 'Número de cuenta', ayuda: 'Solo números, entre 6 y 20 dígitos.', ejemplo: '12345678901', modo: 'numeric', max: 20, filtro: 'digitos' },
        Corriente: { etiqueta: 'Número de cuenta', ayuda: 'Solo números, entre 6 y 20 dígitos.', ejemplo: '12345678901', modo: 'numeric', max: 20, filtro: 'digitos' },
        BilleteraDigital: { etiqueta: 'Celular de la billetera', ayuda: 'El celular registrado en Nequi, Daviplata u otra billetera: 10 dígitos que empiezan por 3.', ejemplo: '3001234567', modo: 'tel', max: 10, filtro: 'digitos' },
        Documento: { etiqueta: 'Llave (NIT)', ayuda: 'Debe ser el NIT de la fundación, sin dígito de verificación: ' + nit + '.', ejemplo: nit, modo: 'numeric', max: 10, filtro: 'digitos' },
        Celular: { etiqueta: 'Llave (celular)', ayuda: '10 dígitos que empiezan por 3.', ejemplo: '3001234567', modo: 'tel', max: 10, filtro: 'digitos' },
        Correo: { etiqueta: 'Llave (correo)', ayuda: 'El correo inscrito como llave en el banco.', ejemplo: 'donaciones@fundacion.org', modo: 'email', max: 60, filtro: 'correo' },
        Alfanumerica: { etiqueta: 'Llave (alfanumérica)', ayuda: 'Empieza con @ y lleva solo letras y números, sin espacios (3 a 20 caracteres).', ejemplo: '@reinogatos', modo: 'text', max: 21, filtro: 'arroba' }
    };

    function reglaActual() {
        if (tipoCuenta.value === 'Llave') return tipoLlave ? reglas[tipoLlave.value] : null;
        return reglas[tipoCuenta.value] || null;
    }

    function aplicar() {
        const esLlave = tipoCuenta.value === 'Llave';
        if (bloqueLlave) bloqueLlave.classList.toggle('d-none', !esLlave);
        const r = reglaActual();
        numero.disabled = !r;
        if (!r) {
            if (etiqueta) etiqueta.textContent = 'Llave o número de cuenta';
            if (ayuda) ayuda.textContent = esLlave ? 'Selecciona el tipo de llave.' : 'Primero selecciona el tipo.';
            return;
        }
        if (etiqueta) etiqueta.textContent = r.etiqueta;
        if (ayuda) ayuda.textContent = r.ayuda;
        numero.placeholder = r.ejemplo;
        numero.inputMode = r.modo;
        numero.maxLength = r.max;
        filtrar();
    }

    function filtrar() {
        const r = reglaActual();
        if (!r) return;
        let v = numero.value;
        if (r.filtro === 'digitos') v = v.replace(/\D/g, '');
        else if (r.filtro === 'correo') v = v.replace(/\s/g, '').toLowerCase();
        else if (r.filtro === 'arroba') {
            v = v.replace(/[^A-Za-z0-9ÁÉÍÓÚÜÑáéíóúüñ]/g, '');
            v = v ? '@' + v : '';
        }
        v = v.slice(0, r.max);
        if (v !== numero.value) numero.value = v;
    }

    tipoCuenta.addEventListener('change', aplicar);
    if (tipoLlave) tipoLlave.addEventListener('change', aplicar);
    numero.addEventListener('input', filtrar);
    aplicar();
})();

// Preguntas condicionales (formulario de adopción, HU-030 a HU-032):
// <div data-mostrar-si="Campo=Valor1,Valor2"> se muestra solo si el campo "Campo" (select, radio o checkbox)
// tiene uno de esos valores. El servidor valida igual: esto solo evita mostrar preguntas que no aplican.
(function () {
    function valoresDe(nombre) {
        return Array.from(document.getElementsByName(nombre))
            .filter(function (c) { return (c.type !== 'radio' && c.type !== 'checkbox') || c.checked; })
            .map(function (c) { return c.value; });
    }

    function actualizar() {
        document.querySelectorAll('[data-mostrar-si]').forEach(function (bloque) {
            var regla = bloque.getAttribute('data-mostrar-si').split('=');
            var esperados = (regla[1] || '').split(',');
            var visible = valoresDe(regla[0]).some(function (v) { return esperados.indexOf(v) >= 0; });
            bloque.classList.toggle('d-none', !visible);
        });
    }

    document.addEventListener('change', function (e) {
        if (e.target && e.target.name) actualizar();
    });
    document.addEventListener('DOMContentLoaded', actualizar);
})();
