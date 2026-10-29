// DEFINIR FUNCIÓN INMEDIATAMENTE - ANTES DEL HTML

const aceScript = document.createElement('script');
aceScript.src = "https://cdnjs.cloudflare.com/ajax/libs/ace/1.4.14/ace.js";
document.head.appendChild(aceScript);

const sqlScript = document.createElement('script');
sqlScript.src = "https://cdnjs.cloudflare.com/ajax/libs/ace/1.4.14/mode-sql.js";
document.head.appendChild(sqlScript);


const themeScript = document.createElement('script');
themeScript.src = "https://cdnjs.cloudflare.com/ajax/libs/ace/1.4.14/theme-textmate.js";
document.head.appendChild(themeScript);

// In your separate JavaScript file (e.g., ace_loader.js)




window.AzureLogsModal = {

    
    loadTaskLog: async function (buildId, logId, taskName, projectType, element) {
        function decorateLog(logText) {
            editor.session.setValue(logText);
            const lines = logText.split('\n');
            lines.forEach((line, idx) => {
                if (line.includes("##[section]")) {
                    editor.session.addGutterDecoration(idx, "ace_section-line");
                }
            });
        }
        function formatLog(log) {
            return log
                .split(/\r?\n/)
                .map(line => {
                    line = line.replace(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z\s*/, '');
                    if (line.startsWith('##[section]Starting:')) return `\n=== ${line.replace('##[section]Starting:', '').trim()} ===`;
                    if (line.startsWith('##[command]')) return '';
                    if (line.startsWith('##[')) return '';
                    if (line.trim().startsWith('-Command')) return '';
                    if (/^=+$/.test(line.trim())) return '';
                    return line;
                })
                .filter(line => line.trim() !== '')
                .join('\n');
        }

        //logs
        const editor = ace.edit("editor");
        editor.setTheme("ace/theme/monokai");
        editor.session.setMode("ace/mode/text");
        editor.setReadOnly(true);

        // Elementos del DOM
        //const logContent = document.getElementById('editor');
        const selectedTaskName = document.getElementById('azSelectedTaskName');
        const logInfo = document.getElementById('azLogInfo');

        if (!selectedTaskName || !logInfo || !editor) {
            console.error('Elementos del modal no encontrados');
            return;
        }

        // Actualizar selección visual
        document.querySelectorAll('.task-item-clickable').forEach(x => x.classList.remove('active'));
        if (element) {
            element.classList.add('active');
        }

        selectedTaskName.textContent = taskName || 'Tarea seleccionada';

        if (!logId || logId === '') {
            //logContent.textContent = 'Esta tarea no tiene log asociado.';
            //logContent.style.color = '#dcdcdc';
            logInfo.textContent = '';
            return;
        }

        // Mostrar loading
        //logContent.textContent = 'Cargando log...';
        //logContent.style.color = '#ffd700';
        logInfo.textContent = `Log ID: ${logId}`;

        try {
            const url = `/pullRequest/GetLog?buildId=${buildId}&logId=${logId}&projectType=${projectType}`;
            console.log('Haciendo fetch a:', url);

            const res = await fetch(url);
            console.log('Response status:', res.status);

            if (!res.ok) {
                const errorText = await res.text();
                console.error('Error response:', errorText);
                logContent.textContent = `Error ${res.status}: ${res.statusText}\n\n${errorText}`;
                logContent.style.color = '#ff6b6b';
                return;
            }

            const text = await res.text();
            console.log('Log cargado exitosamente, caracteres:', text.length);
            decorateLog(formatLog(text));
            //logContent.textContent = decorateLog(formatLog(text)) || 'Log vacío';
            //logContent.style.color = '#dcdcdc';

            // Auto-scroll al final
            setTimeout(() => {
                /*logContent.scrollTop = logContent.scrollHeight;*/
            }, 100);

        } catch (err) {
            console.error('Error cargando log:', err);
            //logContent.textContent = `Error de conexión: ${err.message}`;
            //logContent.style.color = '#ff6b6b';
        }
    }
};



// Función para auto-seleccionar la primera tarea con log
function autoSelectFirstTask() {
    const firstTaskWithLog = document.querySelector('.task-item-clickable[data-haslog="true"]');
    if (firstTaskWithLog) {
        const buildId = firstTaskWithLog.getAttribute('data-buildid');
        const logId = firstTaskWithLog.getAttribute('data-logid');
        const taskName = firstTaskWithLog.getAttribute('data-taskname');
        const projectType = firstTaskWithLog.getAttribute('data-projectType');
        window.AzureLogsModal.loadTaskLog(buildId, parseInt(logId), taskName, projectType, firstTaskWithLog);
    } else {
        console.log('No se encontró ninguna tarea con log disponible');
    }
}

// Configurar cuando el DOM esté listo
document.addEventListener('DOMContentLoaded', function () {

    const modalEl = document.getElementById('azureLogsModal');
    if (modalEl) {
        modalEl.addEventListener('shown.bs.modal', function () {
            console.log('Modal mostrado, auto-seleccionando primera tarea...');
            setTimeout(() => {
                autoSelectFirstTask();
            }, 300);
        });

        modalEl.addEventListener('hidden.bs.modal', function () {
            console.log('Modal ocultado, limpiando...');

            // Limpiar selecciones
            document.querySelectorAll('.task-item-clickable').forEach(x => x.classList.remove('active'));

            // Resetear contenido
            const logContent = document.getElementById('azLogContent');
            const selectedTaskName = document.getElementById('azSelectedTaskName');
            const logInfo = document.getElementById('azLogInfo');

            if (logContent) {
                logContent.textContent = 'Seleccione una tarea de la lista para ver su log';
                logContent.style.color = '#dcdcdc';
            }
            if (selectedTaskName) {
                selectedTaskName.textContent = 'Seleccione una tarea';
            }
            if (logInfo) {
                logInfo.textContent = '';
            }
        });
    }
});