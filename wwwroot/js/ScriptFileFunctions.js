// ********************************************** DROPZONE ******************************************* //
async function setPreview(file) {
    const content = await file.text();

    virtualFiles.push({
        name: file.name,
        content,
        _estado: "nuevo",
        _originalContent: content // también podés guardar esto para futuras comparaciones
    });
    //card.remove();
    renderVirtualFiles();

}

function SetOrderBadges() {
    const container = document.getElementById('virtual-files-container');

    const badges = container.querySelectorAll('.card-position');
    badges.forEach((badge, index) => {
        badge.textContent = `Orden #${index + 1}`;
    });

}

function escapeHtml(text) {
    return text.replace(/[&<>"']/g, function (m) {
        return {
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;'
        }[m];
    });
}

// 🧠 Tu función de hash
async function calcularHashArchivo(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = async function () {
            const buffer = reader.result;
            const hashBuffer = await crypto.subtle.digest('SHA-256', buffer);
            const hashArray = Array.from(new Uint8Array(hashBuffer));
            const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
            resolve(hashHex);
        };
        reader.onerror = reject;
        reader.readAsArrayBuffer(file);
    });
}

async function calcularHashDesdeContenido(texto) {
    const encoder = new TextEncoder();
    const data = encoder.encode(texto); // convierte el string a Uint8Array
    const hashBuffer = await crypto.subtle.digest('SHA-256', data);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
    return hashHex;
}

//dom
document.getElementById("form-virtual-files").addEventListener("submit", function (e) {
    e.preventDefault();
});

let virtualFiles = [];
let dropzoneHashes = [];

function existeArchivoConNombre(nombre) {
    return virtualFiles.some(file => file.name === nombre && file._estado !== "eliminado");
}

/* manejo modal */
function abrirModalArchivo(nombre = "", contenido = "", modo = "nuevo") {
    const modal = new bootstrap.Modal(document.getElementById("modalVirtualFile"));
    const inputNombre = document.getElementById("modalFileName");
    const editorDiv = document.getElementById("modalEditor");

    // Si estás usando ACE Editor u otro, cargalo según tu lógica
    const editor = ace.edit(editorDiv); // Asegurate de que esto apunte al editor real

    // Inicializar ACE solo una vez
    if (!modalEditorInstance) {
        modalEditorInstance = ace.edit("modalEditor");
        modalEditorInstance.setTheme("ace/theme/textmate");
        modalEditorInstance.session.setMode("ace/mode/sql");
        modalEditorInstance.setOptions({
            fontSize: "14px",
            showPrintMargin: false,
            wrap: true
        });
    }

    // Título dinámico
    const titulo = document.getElementById("modalVirtualFileLabel");
    titulo.textContent = modo === "nuevo" ? "Nuevo archivo" : "Editar archivo";

    // Seteá valores iniciales
    inputNombre.value = nombre;
    editor.setValue(contenido, -1);
    editor.setReadOnly(false);

    // Guardar el modo actual (por ejemplo en dataset)
    const btnGuardar = document.getElementById("btnGuardarArchivo");
    btnGuardar.dataset.modo = modo;
    btnGuardar.dataset.nombreOriginal = nombre; // Por si cambia el nombre

    modal.show();
}

document.getElementById("btnGuardarArchivo").addEventListener("click", function () {
    const modo = this.dataset.modo;
    const nombreOriginal = this.dataset.nombreOriginal;
    const nuevoNombre = document.getElementById("modalFileName").value;
    const contenido = ace.edit("modalEditor").getValue();
    var valid = false;
    if (modo === "nuevo") {
        // Agregar nuevo archivo
        valid = agregarArchivoVirtual(nuevoNombre, contenido);
    } else {
        // Editar archivo existente
        valid = actualizarArchivoVirtual(nombreOriginal, nuevoNombre, contenido);
        
    }
    if (valid === true)
        bootstrap.Modal.getInstance(document.getElementById("modalVirtualFile")).hide();

});

async function agregarArchivoVirtual(nuevoNombre, contenido) {
    const name = nuevoNombre.trim();
    const content = contenido.trim();

    if (!name) {
        Swal.fire({
            title: "Error de validación",
            text: "El nombre del archivo es obligatorio.",
            icon: "warning"
        });
        return false;
    }
    if (existeArchivoConNombre(name)) {
        Swal.fire({
            title: "Error de validación",
            text: "El nombre del archivo ya existe en la lista actual.",
            icon: "warning"
        });
        return false;
    }
    if (!content) {
        Swal.fire({
            title: "Error de validación",
            text: "El contenido del archivo es obligatorio.",
            icon: "warning"
        });
        return false;
    }
    var hashFilecontent = await calcularHashDesdeContenido(content);
    if (dropzoneHashes.includes(hashFilecontent)) {
        Swal.fire({
            title: "Error de validación",
            text: "Se detectó un archivo con el mismo contenido.",
            icon: "warning"
        });

        return false;
    }

    dropzoneHashes.push(hashFilecontent);

    virtualFiles.push({
        name,
        content,
        _estado: "nuevo",
        _hash: hashFilecontent
    });
    document.getElementById("btnGuardarArchivo").blur();
    bootstrap.Modal.getInstance(document.getElementById("modalVirtualFile")).hide();
    renderVirtualFiles();
    return true;
}

async function actualizarArchivoVirtual(nombreOriginal, nuevoNombre, nuevoContenido) {
    const archivo = virtualFiles.find(f => f.name === nombreOriginal && f._estado !== "eliminado");

    if (!archivo) {
        console.warn("Archivo no encontrado:", nombreOriginal);
        return false;
    }

    const name = nuevoNombre.trim();
    const content = nuevoContenido.trim();

    if (!name) {
        Swal.fire({
            title: "Error de validación",
            text: "El nombre del archivo es obligatorio.",
            icon: "warning"
        });
        return false;
    }

    // Si se cambió el nombre, verificar duplicados
    if (name !== nombreOriginal && existeArchivoConNombre(name)) {
        Swal.fire({
            title: "Error de validación",
            text: "El nombre del archivo ya existe en la lista actual.",
            icon: "warning"
        });
        return false;
    }

    if (!content) {
        Swal.fire({
            title: "Error de validación",
            text: "El contenido del archivo es obligatorio.",
            icon: "warning"
        });
        return false;
    }

    // Calcular nuevo hash
    const nuevoHash = await calcularHashDesdeContenido(content);

    // Si cambió el contenido, verificar si ya existe ese hash en otro archivo
    if (nuevoHash !== archivo._hash && dropzoneHashes.includes(nuevoHash)) {
        Swal.fire({
            title: "Error de validación",
            text: "Se detectó un archivo con el mismo contenido.",
            icon: "warning"
        });
        return false;
    }

    // Actualizar campos
    archivo.name = name;
    archivo.content = content;

    if (nuevoHash !== archivo._hash) {
        archivo._hash = nuevoHash;
    }

    if (archivo._estado === "sinCambios" && content !== archivo._originalContent) {
        archivo._estado = "modificado";
    } else if (archivo._estado === "sinCambios" && nombreOriginal !== name) {
        archivo._estado = "sinCambios";
    }

    // Ocultar modal y refrescar UI
    document.getElementById("btnGuardarArchivo").blur();
    bootstrap.Modal.getInstance(document.getElementById("modalVirtualFile")).hide();
    renderVirtualFiles();
    return true;
}

function obtenerDatosDelArchivo(boton) {
    const div = boton.closest(".rendered-file"); // asumí que tu template usa esta clase
    const name = div.querySelector(".rendered-name").textContent.trim();
    const editorContainer = div.querySelector(".ace-rendered");
    const editor = ace.edit(editorContainer);
    const content = editor.getValue();

    return {
        nombre: name,
        contenido: content
    };
}


function renderVirtualFiles() {
    const container = document.getElementById("virtual-files-container");
    container.innerHTML = "";

    if (virtualFiles.length > 0) {

        virtualFiles.forEach((file, index) => {
            const templateHtml = document.getElementById("template-rendered-file").innerHTML;
            const wrapper = document.createElement("div");
            wrapper.innerHTML = templateHtml;

            const div = wrapper.firstElementChild;
            const nameEl = div.querySelector(".rendered-name");
            nameEl.textContent = file.name;

            const estadoBadge = div.querySelector(".estado-indicador");

            const btnEdit = div.querySelector(".btn-edit");
            const btnSave = div.querySelector(".btn-save");
            const btnCancel = div.querySelector(".btn-cancel");
            const btnRemove = div.querySelector(".btn-remove");

            const aceContainer = div.querySelector(".ace-rendered");
            const editor = ace.edit(aceContainer);
            editor.setTheme("ace/theme/sqlserver");
            editor.session.setMode("ace/mode/sql");
            editor.setValue(file.content, -1);
            editor.setReadOnly(true);
            editor.setOptions({
                highlightActiveLine: false,
                highlightGutterLine: false,
                showPrintMargin: false
            });
            editor.renderer.$cursorLayer.element.style.display = "none";
            editor.resize();

            // Estado inicial
            file._estado = file._estado || "sinCambios"; // nuevo, modificado, eliminado, sinCambios
            file._originalContent = file.content;

            function actualizarEstado() {
                if (file._estado === "nuevo" || file._estado === "eliminado") return;
                const actual = editor.getValue();
                file._estado = (actual !== file._originalContent) ? "modificado" : "sinCambios";
                actualizarBadge();
            }

            function actualizarBadge() {
                if (file._estado !== "sinCambios") {
                    estadoBadge.textContent = file._estado;
                    estadoBadge.classList.remove("d-none");
                    if (file._estado === "eliminado") {
                        div.classList.add("opacity-50");
                    }
                } else {
                    estadoBadge.classList.add("d-none");
                }
            }

            // btnEdit.addEventListener("click", function (e) {
            //     e.preventDefault(); // <-- esto evita la validación del form
            //     editor.setReadOnly(false);
            //     editor.renderer.$cursorLayer.element.style.display = "";
            //     btnEdit.classList.add("d-none");
            //     btnSave.classList.remove("d-none");
            //     btnCancel.classList.remove("d-none");
            // });

            btnEdit.addEventListener("click", function (e) {
                e.preventDefault();
                const name = nameEl.textContent.trim();
                const content = editor.getValue();
                //const archivo = obtenerDatosDelArchivo(this);
                abrirModalArchivo(name, content, "editar");
            });

            btnSave.addEventListener("click", function (e) {
                e.preventDefault(); // <-- esto evita la validación del form
                file.content = editor.getValue();
                actualizarEstado();
                editor.setReadOnly(true);
                editor.renderer.$cursorLayer.element.style.display = "none";
                btnEdit.classList.remove("d-none");
                btnSave.classList.add("d-none");
                btnCancel.classList.add("d-none");
            });

            btnCancel.addEventListener("click", function (e) {
                e.preventDefault(); // <-- esto evita la validación del form
                editor.setValue(file._originalContent, -1);
                editor.setReadOnly(true);
                editor.renderer.$cursorLayer.element.style.display = "none";
                btnEdit.classList.remove("d-none");
                btnSave.classList.add("d-none");
                btnCancel.classList.add("d-none");
            });

            btnRemove.addEventListener("click", function (e) {
                e.preventDefault(); // <-- esto evita la validación del form
                if (file._estado === "nuevo") {
                    div.remove(); // nunca fue guardado
                    virtualFiles.splice(index, 1);
                } else {
                    file._estado = "eliminado";
                    div.classList.add("opacity-50");
                    actualizarBadge();
                }
            });

            actualizarBadge();
            container.appendChild(div);
        });
        SetOrderBadges();
    }
    else {
        const templateHtml = document.getElementById("template-rendered-file-none").innerHTML;
        const wrapper = document.createElement("div");
        wrapper.innerHTML = templateHtml;
        container.appendChild(wrapper.firstElementChild);

    }
}


function removeVirtualFile(index) {
    virtualFiles.splice(index, 1);
    renderVirtualFiles();
}

function escapeHtml(unsafe) {
    return unsafe.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

let modalEditorInstance;

async function addTemplateFileModal(templateUrl) {
    const response = await fetch(templateUrl);
    const templateContent = await response.text();

    abrirModalArchivo("", templateContent, "nuevo");
}



async function addTemplateFile(templateUrl) {

    const response = await fetch(templateUrl);
    const templateContent = await response.text();

    const container = document.getElementById("virtual-files-container-add");
    const wrapper = document.createElement("div");
    wrapper.innerHTML = document.getElementById("template-editor").innerHTML;
    const editorCard = wrapper.firstElementChild;

    container.appendChild(editorCard);

    // Inicializar ACE en esta instancia
    const aceContainer = editorCard.querySelector(".editor");
    const editor = ace.edit(aceContainer);
    editor.setTheme("ace/theme/textmate");
    editor.session.setMode("ace/mode/sql");
    editor.setValue(templateContent, -1); // -1 para mover cursor al inicio

    // Guardar referencia al editor en el DOM para validación posterior
    editorCard._aceInstance = editor;
}

async function validateAndAdd(button) {
    const card = button.closest(".card");
    const name = card.querySelector(".file-name").value.trim();
    //const content = card.querySelector(".file-content").value.trim();
    const editor = card._aceInstance;
    const content = editor.getValue().trim();

    if (!name) {
        Swal.fire({
            title: "Error de validación",
            text: "El nombre del archivo es obligatorio.",
            icon: "warning"
        });

        return;
    }
    if (existeArchivoConNombre(name)) {
        Swal.fire({
            title: "Error de validación",
            text: "El nombre del archivo ya existe en la lista actual.",
            icon: "warning"
        });
        return;
    }
    if (!content) {
        Swal.fire({
            title: "Error de validación",
            text: "El contenido del archivo es obligatorio.",
            icon: "warning"
        });
        return;
    }
    var hashFilecontent = await calcularHashDesdeContenido(content);
    if (dropzoneHashes.includes(hashFilecontent)) {
        Swal.fire({
            title: "Error de validación",
            text: "Se detectó un archivo con el mismo contenido.",
            icon: "warning"
        });

        return;
    }

    dropzoneHashes.push(hashFilecontent);

    virtualFiles.push({
        name,
        content,
        _estado: "nuevo",
        _originalContent: content // también podés guardar esto para futuras comparaciones
    });
    card.remove();
    renderVirtualFiles();
}

document.querySelectorAll(".template-option").forEach(el => {
    el.addEventListener("click", e => {
        e.preventDefault();
        const templateUrl = el.getAttribute("data-template");
        //addTemplateFile(templateUrl);
        addTemplateFileModal(templateUrl);
    });
});


function cancelAdd(button) {
    const card = button.closest(".card");
    if (card) card.remove();
}