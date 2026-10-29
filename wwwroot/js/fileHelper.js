const archivoHelper = (() => {

    const extensionesPermitidas = ['.sql', '.txt'];

    function obtenerExtension(nombre) {
        const match = nombre.match(/\.[^\.]+$/);
        return match ? match[0].toLowerCase() : '';
    }

    function esExtensionValida(nombre) {
        const ext = obtenerExtension(nombre);
        return extensionesPermitidas.includes(ext);
    }

    function validarNombreArchivo(nombre, nombresExistentes = []) {
        if (!nombre || nombre.trim() === "") {
            return "El nombre del archivo no puede estar vacío.";
        }

        if (!esExtensionValida(nombre)) {
            return `Extensión inválida. Solo se permiten: ${extensionesPermitidas.join(", ")}`;
        }

        if (nombresExistentes.includes(nombre.trim())) {
            return "Ya existe un archivo con ese nombre.";
        }

        return null; // sin errores
    }

    function validarContenidoArchivo(contenido) {
        if (!contenido || contenido.trim() === "") {
            return "El contenido del archivo no puede estar vacío.";
        }

        return null;
    }

    return {
        validarNombreArchivo,
        validarContenidoArchivo,
        extensionesPermitidas
    };
})();