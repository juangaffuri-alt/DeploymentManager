let virtualFiles = [];
let dropzoneHashes = [];
let zipFilesData = []; // Para almacenar datos del ZIP actual
let currentZipStats = {
    totalFiles: 0,
    totalSize: 0,
    selectedCount: 0,
    selectedSize: 0
};

// Prevenir envío del formulario por defecto
document.getElementById("form-virtual-files").addEventListener("submit", function (e) {
    e.preventDefault();
});

function existeArchivoConNombre(nombre) {
    return virtualFiles.some(file => file.name === nombre && file._estado !== "eliminado");
}

// Función para actualizar el contador de archivos
function updateFilesCounter() {
    const activeFiles = virtualFiles.filter(file => file._estado !== "eliminado");
    const counter = document.getElementById("files-counter");
    const count = activeFiles.length;

    if (count === 0) {
        counter.textContent = "0 archivos";
        counter.className = "badge bg-secondary ms-2";
    } else {
        counter.textContent = `${count} archivo${count !== 1 ? 's' : ''}`;
        counter.className = "badge bg-success ms-2";
    }

    // Mostrar información adicional si hay archivos de ZIP
    const zipFiles = activeFiles.filter(file => file._origenZip);
    if (zipFiles.length > 0) {
        const zipGroups = {};
        zipFiles.forEach(file => {
            const zipName = file._zipName || 'ZIP';
            zipGroups[zipName] = (zipGroups[zipName] || 0) + 1;
        });

        const zipInfo = Object.entries(zipGroups).map(([name, count]) =>
            `${count} de ${name}`
        ).join(', ');

        counter.title = `Total: ${count} archivos (${zipInfo})`;
    } else {
        counter.title = `${count} archivo${count !== 1 ? 's' : ''} agregado${count !== 1 ? 's' : ''}`;
    }
}

// Función principal para renderizar archivos como resumen (sin orden y sin vista previa)
function renderVirtualFiles() {
    const container = document.getElementById("virtual-files-container");
    container.innerHTML = "";

    const activeFiles = virtualFiles.filter(file => file._estado !== "eliminado");

    if (activeFiles.length > 0) {
        activeFiles.forEach((file) => {
            const templateHtml = document.getElementById("template-file-summary").innerHTML;
            const wrapper = document.createElement("div");
            wrapper.innerHTML = templateHtml;
            const div = wrapper.firstElementChild;

            // Configurar elementos
            const fileIcon = div.querySelector(".file-icon");
            const fileName = div.querySelector(".file-name");
            const fileSource = div.querySelector(".file-source");
            const filePath = div.querySelector(".file-path");
            const fileSize = div.querySelector(".file-size");
            const fileType = div.querySelector(".file-type");
            const fileDate = div.querySelector(".file-date");
            const btnRemove = div.querySelector(".btn-remove");

            // Llenar información
            fileIcon.className = `${obtenerIconoArchivo(obtenerExtension(file.name))} me-2`;
            fileName.textContent = file.displayName || file.name;
            filePath.textContent = file.rutaOriginal || file.name;
            fileSize.textContent = formatearTamaño(file._tamaño || 0);
            fileType.textContent = obtenerTipoArchivo(file.name);
            fileDate.textContent = file._fecha ? new Date(file._fecha).toLocaleString() : new Date().toLocaleDateString();

            // Configurar fuente
            if (file._origenZip) {
                fileSource.textContent = "ZIP";
                fileSource.className = "badge bg-warning ms-2 file-source";
            } else if (file.origin === "devops") {
                fileSource.textContent = "DevOps";
                fileSource.className = "badge bg-success ms-2 file-source";
            } else {
                fileSource.textContent = "Manual";
                fileSource.className = "badge bg-secondary ms-2 file-source";
            }

            // Event listener para eliminar
            btnRemove.addEventListener("click", function (e) {
                e.preventDefault();
                const realIndex = virtualFiles.findIndex(f => f === file);
                if (file._estado === "nuevo") {
                    virtualFiles.splice(realIndex, 1);
                } else {
                    file._estado = "eliminado";
                }
                renderVirtualFiles();
                updateFilesCounter();
            });

            container.appendChild(div);
        });
    } else {
        const templateHtml = document.getElementById("template-no-files").innerHTML;
        const wrapper = document.createElement("div");
        wrapper.innerHTML = templateHtml;
        container.appendChild(wrapper.firstElementChild);
    }

    updateFilesCounter();
}

// Función para actualizar estadísticas del modal
function updateModalStats() {
    document.getElementById("totalFilesCount").textContent = currentZipStats.totalFiles;
    document.getElementById("selectedFilesCount").textContent = currentZipStats.selectedCount;
    document.getElementById("totalSizeDisplay").textContent = formatearTamaño(currentZipStats.totalSize);
    document.getElementById("selectedSizeDisplay").textContent = formatearTamaño(currentZipStats.selectedSize);

    // Actualizar botón de agregar
    const btnAgregar = document.getElementById("btnAgregarSeleccionados");
    const selectionSummary = document.getElementById("selectionSummary");

    if (currentZipStats.selectedCount === 0) {
        btnAgregar.disabled = true;
        btnAgregar.innerHTML = '<i class="fas fa-plus me-1"></i>Agregar seleccionados';
        selectionSummary.textContent = "Selecciona al menos un archivo para continuar";
    } else {
        btnAgregar.disabled = false;
        btnAgregar.innerHTML = `<i class="fas fa-plus me-1"></i>Agregar ${currentZipStats.selectedCount} archivo${currentZipStats.selectedCount !== 1 ? 's' : ''}`;
        selectionSummary.innerHTML = `<strong>${currentZipStats.selectedCount} archivo${currentZipStats.selectedCount !== 1 ? 's' : ''}</strong> seleccionado${currentZipStats.selectedCount !== 1 ? 's' : ''} (${formatearTamaño(currentZipStats.selectedSize)})`;
    }
}

// Función para calcular estadísticas
function calculateZipStats() {
    const checkboxes = document.querySelectorAll('.zip-file-checkbox');
    const checkedCheckboxes = document.querySelectorAll('.zip-file-checkbox:checked');

    currentZipStats.totalFiles = zipFilesData.length;
    currentZipStats.selectedCount = checkedCheckboxes.length;

    currentZipStats.totalSize = zipFilesData.reduce((sum, file) => sum + file.tamaño, 0);
    currentZipStats.selectedSize = Array.from(checkedCheckboxes).reduce((sum, checkbox) => {
        const index = parseInt(checkbox.value);
        return sum + (zipFilesData[index]?.tamaño || 0);
    }, 0);

    updateModalStats();
}

// ================= ZIP =====================

/**
 * Reemplazado para usar zip.js con soporte opcional de password.
 * - Intenta abrir sin password.
 * - Si hay entradas encriptadas o falla, solicita password y reintenta.
 */
async function procesarArchivoComprimido(file) {
    try {
        const arrayBuffer = await file.arrayBuffer();

        if (file.name.toLowerCase().endsWith('.zip')) {
            let entries = null;
            let zipReader = null;

            // Intento sin password
            try {
                zipReader = new window.zip.ZipReader(new window.zip.BlobReader(file));
                entries = await zipReader.getEntries();

                // Si alguna entrada marca encrypted, tratamos como protegido
                if (entries.some(e => e.encrypted)) {
                    await zipReader.close();
                    entries = null;
                }
            } catch {
                entries = null;
            }

            // Si requiere password o falló, pedimos contraseña
            if (!entries) {
                const { value: password } = await Swal.fire({
                    title: "ZIP protegido",
                    input: "password",
                    inputLabel: "Ingrese la contraseña para abrir el ZIP",
                    inputPlaceholder: "Password",
                    inputAttributes: { autocapitalize: "off" },
                    inputValidator: (value) => {
                        if (!value) return "Debe ingresar una contraseña";
                    },
                    showCancelButton: true,
                    confirmButtonText: "Abrir"
                });

                if (!password) return;

                zipReader = new window.zip.ZipReader(new window.zip.BlobReader(file), { password });
                entries = await zipReader.getEntries();
            }

            await mostrarModalSeleccionArchivos(entries, file.name);

            if (zipReader) await zipReader.close();
        } else {
            Swal.fire({
                title: "Formato no soportado completamente",
                html: `<p>El archivo <strong>${file.name}</strong> es de formato ${file.name.split('.').pop().toUpperCase()}.</p>
                       <p>Por el momento, solo se puede extraer automáticamente el contenido de archivos ZIP.</p>`,
                icon: "info",
                confirmButtonText: "Entendido"
            });
        }
    } catch (error) {
        console.error('Error procesando archivo comprimido:', error);
        Swal.fire({
            title: "Error",
            text: `No se pudo procesar el archivo ${file.name}. ${error.message}`,
            icon: "error"
        });
    }
}

// Función para mostrar modal de selección de archivos del ZIP (adaptada a zip.js)
async function mostrarModalSeleccionArchivos(entries, nombreZip) {
    zipFilesData = [];

    // Extraer información de todos los archivos
    for (const entry of entries) {
        if (!entry.directory) {
            zipFilesData.push({
                nombre: entry.filename,
                ruta: entry.filename,
                tamaño: entry.uncompressedSize || 0,
                fecha: entry.lastModDate,
                zipEntry: entry
            });
        }
    }

    if (zipFilesData.length === 0) {
        Swal.fire({
            title: "ZIP vacío",
            text: "El archivo ZIP no contiene archivos válidos.",
            icon: "warning"
        });
        return;
    }

    // Inicializar estadísticas
    currentZipStats = {
        totalFiles: zipFilesData.length,
        totalSize: zipFilesData.reduce((sum, file) => sum + file.tamaño, 0),
        selectedCount: zipFilesData.length, // Por defecto todos seleccionados
        selectedSize: zipFilesData.reduce((sum, file) => sum + file.tamaño, 0)
    };

    // Crear HTML para el modal
    const archivosHtml = zipFilesData.map((archivo, index) => {
        const extension = obtenerExtension(archivo.nombre);
        const icono = obtenerIconoArchivo(extension);
        const tamaño = formatearTamaño(archivo.tamaño);

        return `
            <div class="file-item p-3 m-2" data-index="${index}">
                <div class="form-check d-flex align-items-center">
                    <input class="form-check-input zip-file-checkbox me-3"
                           type="checkbox"
                           value="${index}"
                           id="archivo_${index}"
                           checked
                           onchange="calculateZipStats(); toggleFileItemSelection(this)">
                    <div class="flex-grow-1">
                        <label class="form-check-label d-flex justify-content-between align-items-center w-100" for="archivo_${index}">
                            <div class="d-flex align-items-center">
                                <i class="${icono} me-3" style="font-size: 1.2rem; width: 20px;"></i>
                                <div>
                                    <div class="fw-bold text-dark">${escapeHtml(archivo.nombre)}</div>
                                    <small class="text-muted">${escapeHtml(archivo.ruta)}</small>
                                </div>
                            </div>
                            <div class="text-end ms-3">
                                <div class="fw-bold text-primary">${tamaño}</div>
                                <small class="text-muted">${archivo.fecha.toLocaleString()}</small>
                            </div>
                        </label>
                    </div>
                </div>
            </div>
        `;
    }).join('');

    // Mostrar modal
    const modal = document.getElementById('modalZipContent');
    const modalTitle = document.getElementById('modalZipContentLabel');
    const zipFileInfo = document.getElementById('zipFileInfo');
    const filesList = document.getElementById('zip-files-list');

    modalTitle.textContent = `Contenido de ${nombreZip}`;
    zipFileInfo.textContent = `${zipFilesData.length} archivos encontrados • ${formatearTamaño(currentZipStats.totalSize)}`;
    filesList.innerHTML = archivosHtml;

    // Actualizar estadísticas iniciales
    updateModalStats();

    const modalInstance = new bootstrap.Modal(modal);
    modalInstance.show();
}

// Función para alternar selección visual
function toggleFileItemSelection(checkbox) {
    const fileItem = checkbox.closest('.file-item');
    if (checkbox.checked) {
        fileItem.classList.add('selected');
    } else {
        fileItem.classList.remove('selected');
    }
}


// Función auxiliar
function arrayBufferToBinaryString(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return binary;
}
// Función auxiliar para grandes archivos:
function arrayBufferToBase64(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    const chunkSize = 32768; // Procesar en chunks de 32KB

    for (let i = 0; i < bytes.length; i += chunkSize) {
        const chunk = bytes.subarray(i, i + chunkSize);
        binary += String.fromCharCode.apply(null, chunk);
    }

    return btoa(binary);
}
// Función para procesar archivos seleccionados (adaptada a zip.js)
async function procesarArchivosSeleccionados(archivos, indices) {
    const loadingSwal = Swal.fire({
        title: 'Procesando archivos...',
        text: 'Agregando archivos seleccionados...',
        allowOutsideClick: false,
        didOpen: () => Swal.showLoading()
    });

    try {
        const zipName = document.getElementById('modalZipContentLabel').textContent.replace('Contenido de ', '');
        const selectedCount = indices.length;
        const totalCount = zipFilesData.length;

        for (const index of indices) {
            const archivo = archivos[index];

            // Verificar duplicados por nombre
            let nombreFinal = archivo.nombre;
            let contador = 1;
            while (existeArchivoConNombre(nombreFinal)) {
                const extension = obtenerExtension(archivo.nombre);
                const nombreSinExt = archivo.nombre.substring(0, archivo.nombre.lastIndexOf('.'));
                nombreFinal = `${nombreSinExt}_${contador}.${extension}`;
                contador++;
            }

            // Para extensiones binarias conocidas
            const extensionesBinarias = ['jar', 'class', 'dll', 'exe', 'png', 'jpg', 'jpeg', 'gif', 'bmp', 'pdf', 'zip', 'rar', '7z', 'war', 'ear'];
            const extension = archivo.nombre.split('.').pop().toLowerCase();

            if (extensionesBinarias.includes(extension)) {
                // Es binario - tratar como binario
                const blob = await archivo.zipEntry.getData(new window.zip.BlobWriter());
                const arrayBuffer = await blob.arrayBuffer();
                //contenidoCompleto = arrayBufferToBinaryString(arrayBuffer);
                //contenidoCompleto = btoa(String.fromCharCode(...new Uint8Array(arrayBuffer))); // ← KEEP THIS
                contenidoCompleto = arrayBufferToBase64(arrayBuffer);
                esBinario = true;  // ← True porque es binario
            } else {
                // Es texto - intentar leer como texto
                contenidoCompleto = await archivo.zipEntry.getData(new window.zip.TextWriter());
                esBinario = false;  // ← Falso porque es texto
            }
            
            const hash = await calcularHashDesdeContenido(contenidoCompleto);

            if (!dropzoneHashes.includes(hash)) {
                dropzoneHashes.push(hash);

                // Procesar la ruta sin la primera carpeta
                const rutaSinPrimeraCarpeta = removerPrimeraCarpeta(archivo.ruta);

                // Agregar a virtualFiles con el contenido y la ruta procesada
                virtualFiles.push({
                    displayName: obtenerNombreArchivo(nombreFinal),
                    name: rutaSinPrimeraCarpeta,
                    content: contenidoCompleto,
                    rutaOriginal: rutaSinPrimeraCarpeta || nombreFinal,
                    _estado: "nuevo",
                    _hash: hash,
                    _origenZip: true,
                    _zipName: zipName,
                    _zipFileCount: totalCount,
                    _zipSelectedCount: selectedCount,
                    _tamaño: archivo.tamaño,
                    _fecha: archivo.fecha,
                    _esBinario: esBinario,
                    _rutaOriginal: rutaSinPrimeraCarpeta || nombreFinal,
                });
            }
        }

        Swal.close();
        renderVirtualFiles();

        Swal.fire({
            title: "¡Archivos agregados!",
            html: `Se han agregado <strong>${indices.length} archivo${indices.length !== 1 ? 's' : ''}</strong> de <strong>${totalCount}</strong> desde el ZIP.<br>
                   <small class="text-muted">Total en la lista: ${virtualFiles.filter(f => f._estado !== 'eliminado').length} archivos</small>`,
            icon: "success",
            timer: 3000
        });

    } catch (error) {
        Swal.close();
        console.error('Error procesando archivos:', error);
        Swal.fire({
            title: "Error",
            text: `Error al procesar archivos: ${error.message}`,
            icon: "error"
        });
    }
}

// Event listener para el botón de agregar seleccionados
document.getElementById('btnAgregarSeleccionados').addEventListener('click', async function () {
    const checkboxes = document.querySelectorAll('.zip-file-checkbox:checked');
    const indices = Array.from(checkboxes).map(cb => parseInt(cb.value));

    if (indices.length === 0) {
        Swal.fire({
            title: "Sin selección",
            text: "Por favor selecciona al menos un archivo.",
            icon: "warning"
        });
        return;
    }

    await procesarArchivosSeleccionados(zipFilesData, indices);
    bootstrap.Modal.getInstance(document.getElementById('modalZipContent')).hide();
});

// Funciones globales para el modal
window.seleccionarTodos = function (seleccionar) {
    const checkboxes = document.querySelectorAll('.zip-file-checkbox');
    checkboxes.forEach(cb => {
        cb.checked = seleccionar;
        toggleFileItemSelection(cb);
    });
    calculateZipStats();
};

window.invertirSeleccion = function () {
    const checkboxes = document.querySelectorAll('.zip-file-checkbox');
    checkboxes.forEach(cb => {
        cb.checked = !cb.checked;
        toggleFileItemSelection(cb);
    });
    calculateZipStats();
};

window.toggleShowOnlySelected = function () {
    const showOnlySelected = document.getElementById('showOnlySelected').checked;
    const fileItems = document.querySelectorAll('.file-item');

    fileItems.forEach(item => {
        const checkbox = item.querySelector('.zip-file-checkbox');
        if (showOnlySelected) {
            item.style.display = checkbox.checked ? 'block' : 'none';
        } else {
            item.style.display = 'block';
        }
    });
};

// ================= Utils =================

function obtenerExtension(nombreArchivo) {
    return nombreArchivo.split('.').pop().toLowerCase();
}

function obtenerIconoArchivo(extension) {
    const iconos = {
        'txt': 'fas fa-file-alt text-primary',
        'sql': 'fas fa-database text-info',
        'js': 'fab fa-js-square text-warning',
        'css': 'fab fa-css3-alt text-primary',
        'html': 'fab fa-html5 text-danger',
        'json': 'fas fa-file-code text-success',
        'xml': 'fas fa-file-code text-info',
        'class': 'fas fa-file-code text-info',
        'jar':'fas fa-file-code text-info',
        /*'pdf': 'fas fa-file-pdf text-danger',
        'doc': 'fas fa-file-word text-primary',
        'docx': 'fas fa-file-word text-primary',
        'xls': 'fas fa-file-excel text-success',
        'xlsx': 'fas fa-file-excel text-success',*/
        'png': 'fas fa-file-image text-info',
        'jpg': 'fas fa-file-image text-info',
        'jpeg': 'fas fa-file-image text-info',
        'gif': 'fas fa-file-image text-info',
        'zip': 'fas fa-file-archive text-warning',
        'rar': 'fas fa-file-archive text-warning',
        '7z': 'fas fa-file-archive text-warning'
    };
    return iconos[extension] || 'fas fa-file text-secondary';
}

function obtenerTipoArchivo(nombreArchivo) {
    const extension = obtenerExtension(nombreArchivo);
    const tipos = {
        'txt': 'Texto plano',
        'sql': 'SQL Script',
        'js': 'JavaScript',
        'css': 'Hoja de estilo',
        'html': 'HTML',
        'json': 'JSON',
        'xml': 'XML',
        'pdf': 'PDF',
        'doc': 'Word',
        'docx': 'Word',
        'xls': 'Excel',
        'xlsx': 'Excel',
        'png': 'Imagen PNG',
        'jpg': 'Imagen JPG',
        'jpeg': 'Imagen JPEG',
        'gif': 'Imagen GIF',
        'zip': 'Archivo ZIP',
        'rar': 'Archivo RAR',
        '7z': 'Archivo 7Z'
    };
    return tipos[extension] || 'Archivo';
}

function formatearTamaño(bytes) {
    const sizes = ['B', 'KB', 'MB', 'GB'];
    if (bytes === 0) return '0 B';
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return Math.round(bytes / Math.pow(1024, i) * 100) / 100 + ' ' + sizes[i];
}

function escapeHtml(text) {
    return text.replace(/[&<>"']/g, function (m) {
        return {
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;'
        }[m];
    });
}

// Función de hash (RESPECTADA)
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
    const data = encoder.encode(texto);
    const hashBuffer = await crypto.subtle.digest('SHA-256', data);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
    return hashHex;
}

function obtenerNombreArchivo(rutaOFile) {
    // Si es un objeto File
    if (rutaOFile && typeof rutaOFile === 'object' && rutaOFile.name) {
        return rutaOFile.name;
    }

    // Si es un string (ruta)
    if (typeof rutaOFile === 'string') {
        return rutaOFile.split(/[\\\/]/).pop().split('?')[0];
    }

    return '';
}

// Función para remover la primera carpeta del path
function removerPrimeraCarpeta(path) {
    if (!path || typeof path !== 'string') {
        return '';
    }

    const partes = path.split('/').filter(parte => parte !== '');

    if (partes.length <= 1) {
        return '';
    }

    return partes.slice(1).join('/');
}

// Función para procesar archivos dropzone
async function setPreview(file) {
    const fileExtension = obtenerExtension(file.name);

    if (['zip', 'rar', '7z'].includes(fileExtension)) {
        await procesarArchivoComprimido(file);
    } else {
        const content = await file.text();
        virtualFiles.push({
            name: file.name,
            content: '',  // No guardamos contenido en la UI
            _estado: "nuevo",
            _hash: file._customHash,
            _tamaño: file.size,
            _fecha: new Date().toISOString()
        });
    }

    renderVirtualFiles();
}
