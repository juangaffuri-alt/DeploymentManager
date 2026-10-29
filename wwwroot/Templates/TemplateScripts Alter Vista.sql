--------ENCABEZADO DEL SCRIPT-----------------------------------
--INCIDENTE: 
--ANALISTA TECNICO: 
--ANALISTA FUNCIONAL:
--DESCRIPCION: Creación de vista 
--OBSERVACION: 
-----------------------------------------------------------------



IF OBJECT_ID('{schema}.{objectName}') IS NULL 
	Exec('Create View {schema}.{objectName} as select test = 1') -- CREA una VISTA con el mismo nombre pero vacia
GO

Alter View {schema}.{objectName} -- modifica siempre la VISTA, ya sea la creada Vacia o la que ya Existe manteniendo sus permisos
as
-- Codigo Vista
