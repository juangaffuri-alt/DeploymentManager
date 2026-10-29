--------ENCABEZADO DEL SCRIPT-----------------------------------
--INCIDENTE: 
--ANALISTA TECNICO: 
--ANALISTA FUNCIONAL:
--DESCRIPCION: Agrega/elimina/modifica campos a una tabla
--OBSERVACION: 
-----------------------------------------------------------------

IF OBJECT_ID('{schema}.{objectName}') IS NOT NULL --Si existe la tabla
ALTER TABLE {schema}.{objectName} -- Agrega/elimina/modifica los campos en a tabla
-- Codigo de alter table