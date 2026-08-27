/* =====================================================================
   MuuBoi — Seed de animais (5 registros)
   Popula a tabela Animals com: 1 touro, 2 vacas, 1 novilha e 1 bezerra.

   Como rodar:
     sqlcmd -S localhost -d MuuBoiDb -i scripts\seed-animals.sql
   ou cole e execute no SSMS / Azure Data Studio conectado ao MuuBoiDb.

   Observações:
   - Os enums são gravados como INT (ver Domain/Enums):
       Gender:         M = 0, F = 1
       Classification: Calf = 1, Heifer = 2, Steer = 3, Bull = 4, Cow = 5
       Breed:          Holstein=1, Jersey=2, Crossbred=3, BrownSwiss=4,
                       Simmental=5, DairyGir=6, Girolando=7, DairyGuzerat=8, Sindhi=9
       Purpose:        Breeder=1, ReplacementHeifer=2, CullCow=3, HeiferForSale=4
       Origin:         BornOnFarm=1, Purchased=2
   - Multi-tenant: cada animal precisa de um PropertyId válido. O script usa
     a primeira Property cadastrada. Se quiser uma específica, ajuste o
     SELECT de @PropertyId abaixo (ex.: filtrar por Name).
   - Idempotente: não reinsere TagNumbers que já existam.
   ===================================================================== */

SET NOCOUNT ON;

DECLARE @PropertyId UNIQUEIDENTIFIER;
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

-- Pega a primeira propriedade cadastrada.
-- Para escolher uma específica, troque por:  WHERE Name = 'Nome da Fazenda'
SELECT TOP (1) @PropertyId = Id
FROM Properties
ORDER BY CreatedAt;

IF @PropertyId IS NULL
BEGIN
    RAISERROR('Nenhuma Property encontrada. Cadastre uma propriedade/usuario antes de rodar o seed.', 16, 1);
    RETURN;
END

PRINT 'Usando PropertyId = ' + CONVERT(NVARCHAR(36), @PropertyId);

INSERT INTO Animals
    (Name, TagNumber, PropertyTagNumber, Gender, BirthDate, Breed,
     Classification, Purpose, Origin, Notes, PropertyId, IsActive, CreatedAt, UpdatedAt)
SELECT v.Name, v.TagNumber, v.PropertyTagNumber, v.Gender, v.BirthDate, v.Breed,
       v.Classification, v.Purpose, v.Origin, v.Notes, @PropertyId, 1, @Now, NULL
FROM (VALUES
    -- Touro (macho adulto, reprodutor)
    (N'Trovão',   N'000001', N'BR-001', 0, CAST('2021-03-15' AS DATETIME2), 7, 4, NULL, 2, N'Reprodutor principal do rebanho.'),
    -- Vaca 1 (matriz, nascida na fazenda)
    (N'Mimosa',   N'000002', N'BR-002', 1, CAST('2020-06-10' AS DATETIME2), 7, 5, 1,    1, N'Matriz em lactação.'),
    -- Vaca 2 (matriz)
    (N'Estrela',  N'000003', N'BR-003', 1, CAST('2019-09-22' AS DATETIME2), 1, 5, 1,    1, N'Matriz de alta produção.'),
    -- Novilha (fêmea jovem, reposição)
    (N'Aurora',   N'000004', N'BR-004', 1, CAST('2024-11-05' AS DATETIME2), 7, 2, 2,    1, N'Novilha de reposição.'),
    -- Bezerra (fêmea recém-nascida)
    (N'Florzinha',N'000005', N'BR-005', 1, CAST('2026-04-12' AS DATETIME2), 7, 1, NULL, 1, N'Bezerra nascida nesta safra.')
) AS v(Name, TagNumber, PropertyTagNumber, Gender, BirthDate, Breed, Classification, Purpose, Origin, Notes)
WHERE NOT EXISTS (
    SELECT 1 FROM Animals a
    WHERE a.PropertyId = @PropertyId AND a.TagNumber = v.TagNumber
);

PRINT CONVERT(NVARCHAR(10), @@ROWCOUNT) + ' animal(is) inserido(s).';
GO
