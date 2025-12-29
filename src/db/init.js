const mysql = require('mysql2/promise');
const { dbConfig, dbName, pool } = require('./mysql');

async function initDatabase() {
  let connection;
  try {
    console.log('⏳ Verificando/Criando banco de dados e tabelas...');
    
    // 1. Conecta sem especificar o banco de dados para criá-lo se não existir
    connection = await mysql.createConnection(dbConfig);
    
    await connection.query(`CREATE DATABASE IF NOT EXISTS \`${dbName}\`;`);
    console.log(`✅ Banco de dados "${dbName}" verificado/criado.`);
    
    // 2. Usa o pool principal (que já tem o banco de dados selecionado) para criar a tabela
    const createProxiesTable = `
      CREATE TABLE IF NOT EXISTS proxies (
        id VARCHAR(36) PRIMARY KEY,
        domain VARCHAR(255) UNIQUE NOT NULL,
        target_host VARCHAR(255) NOT NULL,
        target_port INT,
        enabled TINYINT(1) DEFAULT 0,
        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
      ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    `;

    await pool.query(createProxiesTable);
    console.log('✅ Tabela "proxies" verificada/criada com sucesso.');
    
  } catch (error) {
    console.error('❌ Erro ao inicializar banco de dados:', error);
    throw error;
  } finally {
    if (connection) await connection.end();
  }
}

module.exports = initDatabase;