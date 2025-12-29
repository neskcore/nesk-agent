const express = require("express");
const cors = require("cors");
const path = require("path");
require("dotenv").config();

const apiRoutes = require("./api/routes");
const initDatabase = require("./db/init");

// --- Configura��o do Agent API (Porta 4000) ---
const app = express();
const port = process.env.PORT || 4000;

app.use(cors());
app.use(express.json({ limit: '200mb' }));
app.use(express.urlencoded({ extended: true, limit: '200mb' }));

app.get("/", (req, res) => {
  res.json({ 
    success: true, 
    message: "Nesk Agent Operando normalmente",
    version: "1.0.0",
    status: "online",
    timestamp: new Date() 
  });
});

app.get("/health", (req, res) => {
  res.json({ 
    success: true, 
    message: "Nesk Agent está operante", 
    timestamp: new Date() 
  });
});

app.use("/api", apiRoutes);

app.use((err, req, res, next) => {
  console.error(`[SERVER ERROR] ${err.stack}`);
  const status = err.status || 500;
  res.status(status).json({
    success: false,
    error: process.env.NODE_ENV === "production" ? "Erro interno no servidor" : err.message
  });
});

// --- Configura��o do CDN (Porta 4001) ---
const cdnApp = express();
const cdnPort = process.env.CDN_PORT || 4001;

cdnApp.use(cors());

const attachmentsPath = path.join(__dirname, "cdn", "attachments");

cdnApp.use("/attachments", express.static(attachmentsPath, {
  maxAge: "1d",
  index: false
}));

cdnApp.get("/", (req, res) => {
  res.json({ 
    success: true, 
    message: "Nesk Agent Operando normalmente",
    serving: "/attachments",
    status: "online",
    timestamp: new Date()
  });
});

cdnApp.get("/health", (req, res) => {
  res.json({ 
    success: true, 
    message: "Nesk Agent está operante",
    serving: "/attachments"
  });
});

/**
 * Inicializa��o dos Servidores
 */
async function startServer() {
  try {
    // Inicializa o banco de dados antes de subir os servidores
    await initDatabase();
    
    // Inicia o Agente
    app.listen(port, () => {
      console.log("-------------------------------------------");
      console.log(`?? Nesk Agent iniciado na porta ${port}`);
      console.log(`?? Data: ${new Date().toLocaleString()}`);
      console.log("-------------------------------------------");
    });

    // Inicia o CDN
    cdnApp.listen(cdnPort, () => {
      console.log("-------------------------------------------");
      console.log(`?? Nesk CDN iniciado na porta ${cdnPort}`);
      console.log(`?? Data: ${new Date().toLocaleString()}`);
      console.log("-------------------------------------------");
    });

  } catch (error) {
    console.error("? Falha cr�tica ao iniciar o servidor:", error);
    process.exit(1);
  }
}

startServer();
