--------ENCABEZADO DEL SCRIPT-----------------------------------
--INCIDENTE: 
--ANALISTA TECNICO: 
--ANALISTA FUNCIONAL:
--DESCRIPCION: Inserta datos
--OBSERVACION: 
-----------------------------------------------------------------

----DESARROLLO DEL SCRIPT ----------------------------------------
BEGIN TRAN 
BEGIN TRY
		--Bloque de sentencias
		COMMIT TRAN
END TRY
BEGIN CATCH
      SELECT ERROR_MESSAGE() 
      ROLLBACK TRAN
END CATCH

