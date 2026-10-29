(function () {
    "use strict";

    var selector = ".js-compartir-teams";

    function mostrarAviso(icono, titulo, texto) {
        if (typeof Swal === "undefined") {
            return;
        }

        Swal.fire({
            icon: icono,
            title: titulo,
            text: texto,
            toast: true,
            position: "top-end",
            showConfirmButton: false,
            timer: 4000,
            timerProgressBar: true
        });
    }

    async function copiarAlPortapapeles(texto) {
        if (!texto || !navigator.clipboard || !window.isSecureContext) {
            return false;
        }

        try {
            await navigator.clipboard.writeText(texto);
            return true;
        } catch {
            return false;
        }
    }

    document.addEventListener("click", function (event) {
        var destino = event.target;
        if (!(destino instanceof Element)) {
            return;
        }

        var link = destino.closest(selector);
        if (!link) {
            return;
        }

        var estadoUrl = link.getAttribute("data-estado-url") || "";
        copiarAlPortapapeles(estadoUrl).then(function (copiado) {
            if (copiado) {
                mostrarAviso(
                    "success",
                    "Link copiado",
                    "Elegí el chat o canal en Teams. Si no se abre, pegá el link."
                );
                return;
            }

            mostrarAviso(
                "info",
                "Compartir en Teams",
                "Si Teams no se abre, copiá el link de la pestaña nueva."
            );
        });
    });
})();
