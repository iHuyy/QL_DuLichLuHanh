-- This script adds the PAYLOAD column to the HOADON table to store the JSON data used for digital signatures.
-- The ORA-00904 error for "PAYLOAD" indicates this column is missing from the database schema.
ALTER TABLE TADMIN.HOADON ADD (
  PAYLOAD CLOB
);
/

-- Also, ensure the CHUKYSO column, which stores the signature itself, exists and has an appropriate size.
-- The model expects this column, so we are altering it to a larger size just in case it's too small.
-- Using CLOB is safer for base64 encoded signatures which can be long.
ALTER TABLE TADMIN.HOADON MODIFY (
  CHUKYSO CLOB
);
/

COMMIT;

-- After running this script, the "Hoadon" table will correctly match the Entity Framework model,
-- and the error that occurs during tour booking should be resolved.
-- Please execute this script in your Oracle database.
