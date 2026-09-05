-- Reseta o banco E2E para estado inicial (apaga todos os dados)
-- Use este script entre execuções de testes para garantir estado limpo

USE DetranKanban_E2E;
GO

-- Desabilita constraints para facilitar o truncate
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

-- Trunca todas as tabelas (mais rápido que DELETE e reseta identity)
EXEC sp_MSforeachtable 'TRUNCATE TABLE ?';
GO

-- Reabilita constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO

PRINT 'Banco DetranKanban_E2E resetado. Execute as migrations e seed novamente.';
