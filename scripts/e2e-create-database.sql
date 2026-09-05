-- Cria banco de dados dedicado para testes E2E
-- Execute este script no SQL Server Management Studio ou via sqlcmd

USE master;
GO

-- Dropa o banco se existir (cuidado: apaga todos os dados)
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'DetranKanban_E2E')
BEGIN
    ALTER DATABASE DetranKanban_E2E SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE DetranKanban_E2E;
END
GO

-- Cria o banco
CREATE DATABASE DetranKanban_E2E;
GO

-- Configurações recomendadas para ambiente de teste
USE DetranKanban_E2E;
GO

-- Recovery model SIMPLE para testes (não precisa de point-in-time recovery)
ALTER DATABASE DetranKanban_E2E SET RECOVERY SIMPLE;
GO

-- Permissões (ajuste conforme necessário)
-- Se usar autenticação SQL Server, crie o login e usuário:
-- CREATE LOGIN [e2e_user] WITH PASSWORD = 'YourStrongPassword123!';
-- CREATE USER [e2e_user] FOR LOGIN [e2e_user];
-- ALTER ROLE db_owner ADD MEMBER [e2e_user];
GO

PRINT 'Banco DetranKanban_E2E criado com sucesso.';
PRINT 'Próximo passo: execute as migrations do EF Core para criar o schema.';
