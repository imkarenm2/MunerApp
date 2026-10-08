// MunerApp · reglas de validación en el navegador (equivalen a las del servidor en Validacion/*.cs)
(function ($) {
    if (!$ || !$.validator || !$.validator.unobtrusive) return;

    var FORMATO = /^[A-Za-z0-9._%+-]+@([A-Za-z0-9-]+\.)+[A-Za-z]{2,24}$/;
    var MENSAJE = 'Escribe un correo válido, por ejemplo nombre@gmail.com.';
    var CONOCIDOS = ['gmail.com', 'hotmail.com', 'hotmail.es', 'outlook.com', 'outlook.es', 'yahoo.com',
        'yahoo.es', 'live.com', 'icloud.com', 'ucundinamarca.edu.co'];
    var LEGITIMOS = ['mail.com', 'gmx.com', 'aol.com', 'msn.com', 'live.co', 'yahoo.co', 'email.com', 'ymail.com'];
    var EXT_COM = ['com', 'community', 'company', 'computer', 'comcast', 'comsec'];

    function distancia(a, b) {
        var d = [], i, j;
        for (i = 0; i <= a.length; i++) d[i] = [i];
        for (j = 0; j <= b.length; j++) d[0][j] = j;
        for (i = 1; i <= a.length; i++)
            for (j = 1; j <= b.length; j++)
                d[i][j] = Math.min(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
        return d[a.length][b.length];
    }

    function sugerencia(usuario, dominio, sugerir) {
        return sugerir ? 'Revisa el correo: ¿quisiste decir ' + usuario + '@' + dominio + '?' : MENSAJE;
    }

    // Devuelve null si es válido o el mensaje de error
    function validarCorreo(correo, sugerir) {
        if (correo.length > 254 || !FORMATO.test(correo) || correo.indexOf('..') >= 0
            || correo.charAt(0) === '.' || correo.indexOf('.@') >= 0) return MENSAJE;

        var arroba = correo.lastIndexOf('@');
        var usuario = correo.substring(0, arroba);
        var dominio = correo.substring(arroba + 1).toLowerCase();
        if (dominio.split('.').some(function (p) { return p.charAt(0) === '-' || p.slice(-1) === '-'; })) return MENSAJE;

        var punto = dominio.lastIndexOf('.');
        var extension = dominio.substring(punto + 1);
        if (extension.indexOf('com') === 0 && EXT_COM.indexOf(extension) < 0)
            return sugerencia(usuario, dominio.substring(0, punto + 1) + 'com', sugerir);

        if (!sugerir || CONOCIDOS.indexOf(dominio) >= 0 || LEGITIMOS.indexOf(dominio) >= 0) return null;

        for (var i = 0; i < CONOCIDOS.length; i++)
            if (dominio.indexOf(CONOCIDOS[i]) === 0) return sugerencia(usuario, CONOCIDOS[i], true);

        if (dominio.length >= 6) {
            var mejor = null;
            CONOCIDOS.forEach(function (c) {
                var dist = distancia(dominio, c);
                if (dist >= 1 && dist <= 2 && (!mejor || dist < mejor.dist)) mejor = { dominio: c, dist: dist };
            });
            if (mejor) return sugerencia(usuario, mejor.dominio, true);
        }
        return null;
    }

    $.validator.addMethod('correo', function (valor, elemento, sugerir) {
        valor = $.trim(valor);
        if (this.optional(elemento) && !valor) return true;
        var error = validarCorreo(valor, sugerir);
        $(elemento).data('mensaje-correo', error);
        return error === null;
    });

    $.validator.unobtrusive.adapters.add('correo', ['sugerir'], function (opciones) {
        opciones.rules.correo = opciones.params.sugerir === 'true';
        opciones.messages.correo = function () {
            return $(opciones.element).data('mensaje-correo') || opciones.message;
        };
    });
})(window.jQuery);
