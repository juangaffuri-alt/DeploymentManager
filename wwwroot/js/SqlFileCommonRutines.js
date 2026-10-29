// Función para verificar si una estructura ya existe en DevOps
async function checkStructureExists(repositoryId, structureName, structureType) {
    try {
        const response = await fetch('/sqlFiles/SearchStructures', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                repositoryId: repositoryId,
                structureType: structureType,
                searchTerm: structureName
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const structures = await response.json();

        // Buscar coincidencia exacta por nombre
        return structures.some(structure =>
            structure.name.toLowerCase() === structureName.toLowerCase() &&
            structure.type === structureType
        );
    } catch (error) {
        console.error('Error checking structure:', error);
        return false;
    }
}

function renderVirtualFiles(virtualFiles, virtualFileContainerDiv) {
    const container = document.getElementById(virtualFileContainerDiv);

    if (virtualFiles.length === 0) {
        container.innerHTML = `
                                                                                                            <div class="col-12">
                                                                                                                <div class="card">
                                                                                                                    <div class="card-body text-center text-muted">
                                                                                                                        <i class="fas fa-folder-open fa-3x mb-3"></i>
                                                                                                                        <p>No hay scripts agregados</p>
                                                                                                                        <small>Usa las pestañas de arriba para agregar contenido</small>
                                                                                                                    </div>
                                                                                                                </div>
                                                                                                            </div>
                                                                                                        `;
        return;
    }

    const filesArray = virtualFiles
        .filter(file => file._estado !== "eliminado")
        .map((file, index) => {
            return {
                id: file.id,
                html: `<div class="col-12 mb-3 virtual-file-item" data-index="${index}">
                                                                                                            <div class="card">
                                                                                                                <div class="card-header d-flex justify-content-between align-items-center">
                                                                                                                    <div class="d-flex align-items-center">
                                                                                                                        <span class="badge bg-primary me-2" style="cursor: move;" title="Arrastra para reordenar">
                                                                                                                            ${index + 1}
                                                                                                                        </span>
                                                                                                                        <div>
                                                                                                                            <strong>${getStructureIcon(file.structureType || 'sql')} ${file.name}</strong>
                                                                                                                            <div class="small text-muted">
                                                                                                                                ${getOriginBadge(file.origin)}
                                                                                                                                ${file.originalPath ? `• ${file.originalPath}` : ''}
                                                                                                                            </div>
                                                                                                                        </div>
                                                                                                                    </div>
                                                                                                                    <div class="btn-group">
                                                                                                                        <button class="btn btn-sm btn-outline-primary" onclick="editFile(${index})" title="Editar" type="button">
                                                                                                                            <i class="bi bi-pencil"></i>
                                                                                                                        </button>
                                                                                                                        <button class="btn btn-sm btn-outline-info" onclick="previewFile(${index})" title="Vista previa" type="button">
                                                                                                                            <i class="bi bi-eye"></i>
                                                                                                                        </button>
                                                                                                                        <button class="btn btn-sm btn-outline-danger" onclick="removeFile(${index})" title="Eliminar" type="button">
                                                                                                                            <i class="bi bi-trash"></i>
                                                                                                                        </button>
                                                                                                                    </div>
                                                                                                                </div>
                                                                                                                <div class="card-body p-2">
                                                                                                                    <div class="code-preview bg-dark text-light p-2 rounded" style="font-family: 'Courier New', monospace; font-size: 11px; max-height: 120px; overflow-y: auto; line-height: 1.4;">
                                                                                                                        ${escapeHtml(file.content.substring(0, 400))}${file.content.length > 400 ? '\n...' : ''}
                                                                                                                    </div>
                                                                                                                </div>
                                                                                                            </div>
                                                                                                        </div>
                                                                                                    `
            };
        });

    // Cantidad de elementos
    const cantidad = filesArray.length; 

    // Luego convertir a string si es necesario
    const filesHtml = filesArray.map(item => item.html).join('');

    container.innerHTML = filesHtml;
    document.getElementById("filesCount").innerText = cantidad;
}

function detectarModificaciones(sqlContent) {
    const patrones = {
        "ALTER PROCEDURE": /ALTER\s+PROCEDURE/gi,
        "CREATE PROCEDURE": /CREATE\s+PROCEDURE/gi,
        "DROP PROCEDURE": /DROP\s+PROCEDURE/gi,
        "ALTER TABLE": /ALTER\s+TABLE/gi,
        "CREATE TABLE": /CREATE\s+TABLE/gi,
        "DROP TABLE": /DROP\s+TABLE/gi,
        "ALTER FUNCTION": /ALTER\s+FUNCTION/gi,
        "CREATE FUNCTION": /CREATE\s+FUNCTION/gi,
        "DROP FUNCTION": /DROP\s+FUNCTION/gi,
        "ALTER TRIGGER": /ALTER\s+TRIGGER/gi,
        "CREATE TRIGGER": /CREATE\s+TRIGGER/gi,
        "DROP TRIGGER": /DROP\s+TRIGGER/gi,
        "ALTER VIEW": /ALTER\s+VIEW/gi,
        "CREATE VIEW": /CREATE\s+VIEW/gi,
        "DROP VIEW": /DROP\s+VIEW/gi
    };

    const resultados = {};

    for (const [clave, regex] of Object.entries(patrones)) {
        const matches = sqlContent.match(regex);
        resultados[clave] = matches ? matches.length : 0;
    }

    return resultados;
}

// Función para extraer información de la estructura desde el contenido SQL
function parseStructureFromContent(content) {
    if (!content) return { type: '', name: '', schema: 'dbo', isStructure: false };

    const lines = content.split('\n').map(line => line.trim().toUpperCase());

    for (const line of lines) {
        // Buscar CREATE/ALTER statements
        if (line.startsWith('CREATE') || line.startsWith('ALTER')) {
            // Stored Procedure
            var spMatch = line.match(/(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?PROCEDURE\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)/i);
            if (spMatch) {
                return {
                    type: 'StoredProcedure',
                    schema: spMatch[1] || 'dbo',
                    name: spMatch[2],
                    isStructure: true
                };
            }

            spMatch = line.match(/(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?PROC\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)/i);
            if (spMatch) {
                return {
                    type: 'StoredProcedure',
                    schema: spMatch[1] || 'dbo',
                    name: spMatch[2],
                    isStructure: true
                };
            }

            // View
            const viewMatch = line.match(/(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?VIEW\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)/i);
            if (viewMatch) {
                return {
                    type: 'View',
                    schema: viewMatch[1] || 'dbo',
                    name: viewMatch[2],
                    isStructure: true
                };
            }

            // Function
            const funcMatch = line.match(/(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?FUNCTION\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)/i);
            if (funcMatch) {
                return {
                    type: 'Function',
                    schema: funcMatch[1] || 'dbo',
                    name: funcMatch[2],
                    isStructure: true
                };
            }

            // Table
            const tableMatch = line.match(/CREATE\s+TABLE\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)/i);
            if (tableMatch) {
                return {
                    type: 'Table',
                    schema: tableMatch[1] || 'dbo',
                    name: tableMatch[2],
                    isStructure: true
                };
            }

            // Trigger
            const triggerMatch = line.match(/(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?TRIGGER\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)/i);
            if (triggerMatch) {
                return {
                    type: 'Trigger',
                    schema: triggerMatch[1] || 'dbo',
                    name: triggerMatch[2],
                    isStructure: true
                };
            }
        }
    }

    return { type: '', name: '', schema: 'dbo', isStructure: false };
}

// Nueva función para generar el nombre de archivo automáticamente
function generateFileName(structureName, structureType) {
    if (!structureName || !structureType) return '';

    // Limpiar el nombre de la estructura
    const cleanName = structureName.trim().toLowerCase();

    // Extraer schema y nombre
    let schema = 'dbo';
    let name = cleanName;

    if (cleanName.includes('.')) {
        const parts = cleanName.split('.');
        schema = parts[0];
        name = parts.slice(1).join('.');
    }

    // Mapear tipos a carpetas
    const typeToFolder = {
        'StoredProcedure': 'stored_procedures',
        'View': 'views',
        'Function': 'functions',
        'Table': 'tables',
        'Trigger': 'triggers'
    };

    const folder = typeToFolder[structureType] || 'scripts';

    return `/database/${folder}/${schema}/${name}.sql`;
}

// Función para actualizar el nombre del archivo basado en el contenido
function updateFileNameFromContent(currentEditingFile, modalEditor) {
    // No actualizar si es un archivo desde DevOps (para mantener ALTER consistency)
    if (currentEditingFile && currentEditingFile.origin === 'devops') {
        return;
    }

    const content = modalEditor.getValue();
    const parsed = parseStructureFromContent(content);

    if (parsed.name && parsed.type) {
        const fullName = `${parsed.schema}.${parsed.name}`;
        const fileName = generateFileName(fullName, parsed.type);
        //document.getElementById('modalFileName').value = fileName;

        // Actualizar el archivo actual
        if (currentEditingFile) {
            currentEditingFile.autoGeneratedFileName = fileName;
            currentEditingFile.structureType = parsed.type;
            currentEditingFile.parsedStructure = parsed;
        }
    }
}

/* EDITORS */

// Aplicar visibilidad del panel con animaciones
function applyPanelVisibility(visible, editorSettingsCardDivName, showSettingsPanelDivName) {
    const settingsCard = document.getElementById(editorSettingsCardDivName);
    const showBtn = document.getElementById(showSettingsPanelDivName);

    if (visible) {
        settingsCard.style.display = 'block';
        showBtn.style.display = 'none';
    } else {
        settingsCard.style.display = 'none';
        showBtn.style.display = 'inline-block';
    }
}
// modal files manage

// Función MODIFICADA para validar si la estructura ya existe al guardar
async function saveFileFromModal() {
    const content = modalEditor.getValue();

    if (!content.trim()) {
        Swal.fire({
            icon: 'warning',
            title: 'Contenido requerido',
            text: 'Por favor ingresa contenido para el archivo',
            confirmButtonText: 'Entendido'
        });
        return;
    }

    const parsed = parseStructureFromContent(content);

    // ============ VALIDACIÓN DE ESTRUCTURA EXISTENTE ============
    // Solo validar si es un archivo nuevo (estado "nuevo") y se pudo parsear la estructura
    if (currentEditingFile && currentEditingFile._estado === "nuevo" && parsed.name && parsed.type) {
        const repositoryId = document.querySelector('[name="RepositoryId"]').value;

        if (repositoryId) {
            // Verificar si la estructura ya existe en DevOps
            const structureExists = await checkStructureExists(repositoryId, parsed.name, parsed.type);

            if (structureExists) {

                // Para otros tipos (StoredProcedure, View, Function, Trigger)
                const result = await Swal.fire({
                    title: 'Estructura existente',
                    html: `
                                        <p>La estructura <strong>${parsed.name}</strong> (${parsed.type}) ya existe en el repositorio.</p>
                                        <p>¿Deseas continuar con la modificación?</p>
                                    `,
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Sí, continuar',
                    cancelButtonText: 'Cancelar'
                });

                if (result.isConfirmed) {
                    // Cambiar el estado a "modificado"
                    currentEditingFile._estado = "modificado";
                    currentEditingFile.origin = "devops";

                    const updatedContent = content;
                    const updatedParsed = parseStructureFromContent(updatedContent);

                    // ============ GUARDAR AUTOMÁTICAMENTE ============
                    await saveFileProcess(updatedContent, updatedParsed);
                    // ============ CERRAR MODAL AUTOMÁTICAMENTE ============
                    bootstrap.Modal.getInstance(document.getElementById('modalVirtualFile')).hide();
                    return;
                } else {
                    // Si el usuario cancela, no guardar
                    return;
                }
            }
        }
    }
    // // ============ FIN VALIDACIÓN ============

    // Si no hubo validación o no existe la estructura, proceder con el guardado normal
    await saveFileProcess(content, parsed);

    // Cerrar modal
    bootstrap.Modal.getInstance(document.getElementById('modalVirtualFile')).hide();
}

function resolverEstadoArchivo(file, nuevoNombre, nuevoContenido) {
    const estadoActual = file._estado;
    if (estadoActual === "nuevo" || estadoActual === "eliminado") {
        return estadoActual;
    }

    const contenidoOriginal = file.originalContent || file.content;
    const nombreOriginal = file.originalPath || file.name;
    const hayCambio = nuevoContenido !== contenidoOriginal || nuevoNombre !== nombreOriginal;
    if (hayCambio) {
        return "modificado";
    }

    // Archivos ya persistidos (hidratación de Edit): volver a sinCambios si se revirtió el contenido.
    // Archivos agregados en esta sesión como modificado (p. ej. estructura DevOps) no se degradan.
    if (file._persistido) {
        return "sinCambios";
    }

    return estadoActual === "modificado" ? "modificado" : "sinCambios";
}

async function saveFileProcess(content, parsed) {
    const resultado = detectarModificaciones(content);

    // Filtrar claves con más de 2 ocurrencias
    const modificacionesMultiples = Object.entries(resultado)
        .filter(([clave, cantidad]) => cantidad >= 2)
        .map(([clave, cantidad]) => ({ tipo: clave, cantidad }));

    /*if (modificacionesMultiples.length > 0) {
        Swal.fire({
            icon: 'warning',
            title: 'Script inválido',
            text: 'El script contiene más de una sentencia de modificación',
            confirmButtonText: 'Entendido'
        });
        return false;
    }*/

    // Generar nombre de archivo automáticamente
    let fileName = '';
    if (parsed.name && parsed.type) {
        const fullName = `${parsed.schema}.${parsed.name}`;
        fileName = generateFileName(fullName, parsed.type);
    } else if (currentEditingFile) {
        // Si no se puede parsear, usar el nombre existente
        fileName = currentEditingFile.autoGeneratedFileName || currentEditingFile.name;
    }

    if (currentEditingFile) {
        if (currentEditingFile.originalPath != null && fileName.toLowerCase() == currentEditingFile.originalPath.toLowerCase()) {
            fileName = currentEditingFile.originalPath;
        }
        // Actualizar el archivo en edición (identidad) o el que coincida por nombre generado
        let existingIndex = virtualFiles.findIndex(f => f === currentEditingFile);
        if (existingIndex === -1) {
            existingIndex = virtualFiles.findIndex(f => f.name === fileName);
        }
        if (existingIndex !== -1) {
            const originalContent = currentEditingFile.originalContent || currentEditingFile.content;
            virtualFiles[existingIndex] = {
                ...currentEditingFile,
                name: fileName,
                content: content,
                originalContent: originalContent,
                structureType: parsed.type || currentEditingFile.structureType,
                autoGeneratedFileName: fileName,
                parsedStructure: parsed,
                _hash: await calculateHash(content),
                _estado: resolverEstadoArchivo(currentEditingFile, fileName, content)
            };
        } else {
            // Es un archivo nuevo
            function getUniqueFileNameWithExtension(fullFileName, existingFiles) {
                // Separar nombre y extensión
                const lastDotIndex = fullFileName.lastIndexOf('.');
                let baseName, extension;

                if (lastDotIndex === -1) {
                    baseName = fullFileName;
                    extension = '';
                } else {
                    baseName = fullFileName.substring(0, lastDotIndex);
                    extension = fullFileName.substring(lastDotIndex);
                }

                let counter = 0;
                let newName = fullFileName;

                // Verificar si el nombre original ya existe
                if (!fileNameExists(newName, existingFiles)) {
                    return newName;
                }

                // Buscar el siguiente número disponible
                while (true) {
                    counter++;
                    newName = `${baseName}_${counter}${extension}`;

                    if (!fileNameExists(newName, existingFiles)) {
                        return newName;
                    }
                }
            }
            // Función auxiliar para verificar si un nombre ya existe
            function fileNameExists(fileName, files) {
                return files.some(file =>
                    file.name === fileName || file.autoGeneratedFileName === fileName
                );
            }

            // Uso:
            const hash = await calculateHash(content);
            // ojo si llega un hash ya existente, alertamos que ya existe un archivo igual
            const existingIndex = virtualFiles.findIndex(f => f._hash === hash);
            if (existingIndex !== -1) {
                // Mostrar mensaje de éxito
                Swal.fire({
                    icon: 'error',
                    title: '¡Archivo duplicado!',
                    text: `Ya existe un script con el mismo contenido`,
                    timer: 1500,
                    showConfirmButton: false
                });
                return;
            }

            const uniqueFileName = getUniqueFileNameWithExtension(fileName, virtualFiles);
            
            const newFile = {
                ...currentEditingFile,
                name: uniqueFileName,
                content: content,
                originalContent: currentEditingFile.originalContent || currentEditingFile.content,
                structureType: parsed.type || currentEditingFile.structureType,
                autoGeneratedFileName: fileName,
                parsedStructure: parsed,
                _hash: hash
            };
            addVirtualFile(newFile);
        }
    }

    renderVirtualFiles(virtualFiles, 'virtual-files-container');

    // Mostrar mensaje de éxito
    Swal.fire({
        icon: 'success',
        title: '¡Archivo guardado!',
        text: `El archivo "${fileName}" se guardó correctamente`,
        timer: 1500,
        showConfirmButton: false
    });

    return true;
}