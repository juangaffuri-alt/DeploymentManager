(function (window, $) {
    'use strict';

    var MAX_FILAS = 50;
    var MSG_INCOMPLETA = 'Cada tarea del plan de implementación debe tener descripción y responsable.';
    var summernoteOpts = {
        airMode: true,
        popover: {
            air: [
                ['style', ['bold', 'italic', 'underline']],
                ['color', ['color']],
                ['para', ['ul', 'ol']]
            ]
        }
    };

    function parseInitial(json) {
        if (!json || !String(json).trim()) return [];
        try {
            var data = JSON.parse(json);
            return Array.isArray(data) ? data : [];
        } catch (e) {
            return [];
        }
    }

    function toBoolInformar(value) {
        if (value === true || value === 1) return true;
        if (value === false || value === 0 || value == null || value === '') return false;
        if (typeof value === 'string') {
            var lower = value.trim().toLowerCase();
            if (lower === 'true' || lower === '1') return true;
            if (lower === 'false' || lower === '0') return false;
            var text = $('<div>').html(value).text().trim();
            return text.length > 0;
        }
        return !!value;
    }

    function stripHtml(html) {
        return $('<div>').html(html || '').text().trim();
    }

    function renumber($tbody) {
        $tbody.find('tr').each(function (i) {
            $(this).find('.tarea-nro').text(i + 1);
        });
    }

    function initSummernote($el, html) {
        if (!$el.length || typeof $el.summernote !== 'function') {
            $el.val(html || '');
            return;
        }
        $el.summernote(summernoteOpts);
        $el.summernote('code', html || '');
    }

    function destroySummernote($el) {
        if ($el.length && typeof $el.summernote === 'function' && $el.next('.note-editor').length) {
            $el.summernote('destroy');
        }
    }

    function getHtml($el) {
        if ($el.length && typeof $el.summernote === 'function' && $el.next('.note-editor').length) {
            return $el.summernote('code') || '';
        }
        return $el.val() || '';
    }

    function getCollapseEl($wrapper) {
        var id = $wrapper.attr('data-collapse-id');
        return id ? document.getElementById(id) : $wrapper.find('.collapse')[0];
    }

    function getBsCollapse($wrapper) {
        var el = getCollapseEl($wrapper);
        if (!el || typeof bootstrap === 'undefined' || !bootstrap.Collapse) return null;
        return bootstrap.Collapse.getOrCreateInstance(el, { toggle: false });
    }

    function setToggleEnabled($wrapper, enabled) {
        $wrapper.find('.tabla-tareas-toggle').prop('disabled', !enabled);
    }

    function showTable($wrapper) {
        setToggleEnabled($wrapper, true);
        var bs = getBsCollapse($wrapper);
        if (bs) bs.show();
        $wrapper.find('.tabla-tareas-toggle').attr('aria-expanded', 'true');
    }

    function hideTable($wrapper) {
        var bs = getBsCollapse($wrapper);
        if (bs) bs.hide();
        $wrapper.find('.tabla-tareas-toggle').attr('aria-expanded', 'false');
        setToggleEnabled($wrapper, false);
        clearValidation($wrapper);
    }

    function rowCount($wrapper) {
        return $wrapper.find('.tabla-tareas-body tr').length;
    }

    function isRowComplete($tr) {
        var desc = stripHtml(getHtml($tr.find('.tarea-descripcion')));
        var resp = ($tr.find('.tarea-responsable').val() || '').trim();
        return !!desc && !!resp;
    }

    function lastRowComplete($wrapper) {
        var $last = $wrapper.find('.tabla-tareas-body tr').last();
        if (!$last.length) return true;
        return isRowComplete($last);
    }

    function clearValidation($wrapper) {
        $wrapper.find('.tabla-tareas-validation').hide().text('');
        $wrapper.find('.tarea-descripcion, .tarea-responsable').removeClass('is-invalid');
        $wrapper.find('.note-editor').removeClass('border-danger');
    }

    function markRowInvalid($tr) {
        var $desc = $tr.find('.tarea-descripcion');
        var $resp = $tr.find('.tarea-responsable');
        if (!stripHtml(getHtml($desc))) {
            $desc.addClass('is-invalid');
            $desc.next('.note-editor').addClass('border-danger');
        }
        if (!($resp.val() || '').trim()) {
            $resp.addClass('is-invalid');
        }
    }

    function showValidation($wrapper, message) {
        var $msg = $wrapper.find('.tabla-tareas-validation');
        if (!$msg.length) {
            $msg = $('<div class="alert alert-danger py-2 px-3 mt-2 mb-0 tabla-tareas-validation" role="alert"></div>');
            $wrapper.find('.card-body').append($msg);
        }
        $msg.text(message).show();
        showTable($wrapper);
    }

    function updateRemoveButtons($wrapper) {
        var $rows = $wrapper.find('.tabla-tareas-body tr');
        $rows.each(function (i) {
            var isLast = i === $rows.length - 1;
            $(this).find('.btn-remove-tarea').prop('disabled', !isLast).toggleClass('d-none', !isLast);
        });
    }

    function updateAddButton($wrapper) {
        var $btn = $wrapper.find('.btn-add-tarea');
        if (!$btn.length) return;
        var count = rowCount($wrapper);
        var canAdd = count < MAX_FILAS && lastRowComplete($wrapper);
        $btn.prop('disabled', !canAdd);
        $btn.attr('title', canAdd ? '' : (count >= MAX_FILAS
            ? 'Máximo de tareas alcanzado'
            : 'Complete descripción y responsable de la última tarea'));
    }

    function refreshUi($wrapper) {
        updateRemoveButtons($wrapper);
        updateAddButton($wrapper);
    }

    function bindRowEvents($wrapper, $tr) {
        $tr.find('.tarea-responsable').on('input change', function () {
            $(this).removeClass('is-invalid');
            clearValidation($wrapper);
            refreshUi($wrapper);
        });

        var $desc = $tr.find('.tarea-descripcion');
        $desc.on('summernote.change input change', function () {
            $desc.removeClass('is-invalid');
            $desc.next('.note-editor').removeClass('border-danger');
            clearValidation($wrapper);
            refreshUi($wrapper);
        });
    }

    function addRow($wrapper, item, editable) {
        var $tbody = $wrapper.find('.tabla-tareas-body');
        if ($tbody.find('tr').length >= MAX_FILAS) {
            return false;
        }

        var nro = $tbody.find('tr').length + 1;
        var descripcion = (item && item.descripcion) || '';
        var responsable = (item && item.responsable) || '';
        var informar = toBoolInformar(item && item.informarFinalizacion);

        var $tr = $('<tr></tr>');
        $tr.append($('<td class="text-center text-nowrap tarea-nro"></td>').text(nro));

        if (editable) {
            var $desc = $('<textarea class="form-control form-control-sm tarea-descripcion"></textarea>');
            var $resp = $('<input type="text" class="form-control form-control-sm tarea-responsable" maxlength="80" />').val(responsable);
            var $inf = $('<input type="checkbox" class="form-check-input tarea-informar" title="Finalizado" />').prop('checked', informar);
            $tr.append($('<td class="tarea-desc-cell"></td>').append($desc));
            $tr.append($('<td class="text-nowrap"></td>').append($resp));
            $tr.append($('<td class="text-center"></td>').append($inf));
            $tr.append(
                $('<td class="text-center text-nowrap"></td>').append(
                    $('<button type="button" class="btn btn-sm btn-danger btn-remove-tarea" title="Eliminar"><i class="fas fa-trash"></i></button>')
                )
            );
            $tbody.append($tr);
            initSummernote($desc, descripcion);
            bindRowEvents($wrapper, $tr);
        } else {
            $tr.append($('<td class="tarea-desc-cell tarea-descripcion-ro"></td>').html(descripcion));
            $tr.append($('<td class="text-nowrap"></td>').text(responsable));
            var $infRo = $('<input type="checkbox" class="form-check-input" disabled />')
                .prop('checked', informar)
                .attr('title', 'Finalizado');
            $tr.append($('<td class="text-center"></td>').append($infRo));
            $tbody.append($tr);
        }

        renumber($tbody);
        refreshUi($wrapper);
        return true;
    }

    function validateWrapper($wrapper) {
        clearValidation($wrapper);
        var editable = $wrapper.attr('data-editable') === 'true';
        if (!editable) return true;

        var ok = true;
        $wrapper.find('.tabla-tareas-body tr').each(function () {
            var $tr = $(this);
            if (!isRowComplete($tr)) {
                markRowInvalid($tr);
                ok = false;
            }
        });

        if (!ok) {
            showValidation($wrapper, MSG_INCOMPLETA);
        }
        return ok;
    }

    function serializeWrapper($wrapper) {
        var editable = $wrapper.data('editable') === true || $wrapper.attr('data-editable') === 'true';
        if (!editable) return true;

        if (!validateWrapper($wrapper)) {
            return false;
        }

        var rows = [];
        $wrapper.find('.tabla-tareas-body tr').each(function (i) {
            var $tr = $(this);
            rows.push({
                nro: i + 1,
                descripcion: getHtml($tr.find('.tarea-descripcion')),
                responsable: ($tr.find('.tarea-responsable').val() || '').trim(),
                informarFinalizacion: $tr.find('.tarea-informar').is(':checked')
            });
        });

        $wrapper.find('.tareas-json-input').val(rows.length ? JSON.stringify(rows) : '');
        return true;
    }

    function initWrapper($wrapper) {
        if ($wrapper.data('tabla-tareas-ready')) return;
        $wrapper.data('tabla-tareas-ready', true);

        var editable = $wrapper.attr('data-editable') === 'true';
        var initial = parseInitial($wrapper.find('.tareas-json-input').val());

        if (initial.length === 0) {
            if (!editable) {
                $wrapper.hide();
                return;
            }
            hideTable($wrapper);
            refreshUi($wrapper);
        } else {
            initial.forEach(function (item) {
                addRow($wrapper, item, editable);
            });
            showTable($wrapper);
            refreshUi($wrapper);
        }

        var collapseEl = getCollapseEl($wrapper);
        if (collapseEl) {
            collapseEl.addEventListener('show.bs.collapse', function () {
                $wrapper.find('.tabla-tareas-toggle').attr('aria-expanded', 'true');
            });
            collapseEl.addEventListener('hide.bs.collapse', function () {
                $wrapper.find('.tabla-tareas-toggle').attr('aria-expanded', 'false');
            });
        }

        if (!editable) return;

        $wrapper.on('click', '.btn-add-tarea', function () {
            if (!lastRowComplete($wrapper)) {
                var $last = $wrapper.find('.tabla-tareas-body tr').last();
                markRowInvalid($last);
                showValidation($wrapper, MSG_INCOMPLETA);
                return;
            }
            if (addRow($wrapper, null, true)) {
                showTable($wrapper);
                refreshUi($wrapper);
            }
        });

        $wrapper.on('click', '.btn-remove-tarea', function () {
            var $tr = $(this).closest('tr');
            var $last = $wrapper.find('.tabla-tareas-body tr').last();
            if (!$tr.is($last)) {
                return;
            }
            destroySummernote($tr.find('.tarea-descripcion'));
            $tr.remove();
            renumber($wrapper.find('.tabla-tareas-body'));
            if (rowCount($wrapper) === 0) {
                hideTable($wrapper);
            }
            refreshUi($wrapper);
        });

        var $form = $wrapper.closest('form');
        if ($form.length) {
            $form.on('submit.tablaTareas', function (e) {
                if (!serializeWrapper($wrapper)) {
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    return false;
                }
            });
        }
    }

    function initAll() {
        $('.tabla-tareas-wrapper').each(function () {
            initWrapper($(this));
        });
    }

    /** Serializa y valida. Devuelve false si hay filas incompletas. */
    window.serializeTablaTareas = function () {
        var ok = true;
        $('.tabla-tareas-wrapper').each(function () {
            if (!serializeWrapper($(this))) {
                ok = false;
            }
        });
        return ok;
    };

    window.initTablaTareas = initAll;

    $(function () {
        initAll();
    });
})(window, jQuery);
