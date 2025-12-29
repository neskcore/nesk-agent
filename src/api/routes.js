const express = require('express');
const router = express.Router();
const ProxyController = require('./controllers/proxyController');
const CdnController = require('./controllers/cdnController');
const CdnService = require('../services/cdnService');
const authMiddleware = require('./middlewares/auth');

// Todas as rotas do agente requerem autenticação
router.use(authMiddleware);

/**
 * Rotas de Gerenciamento de Proxy
 */
router.post('/proxy', ProxyController.create);
router.get('/proxy', ProxyController.getAll);
router.put('/proxy/:id', ProxyController.update);
router.delete('/proxy/:id', ProxyController.delete);

// Configuração manual do arquivo .conf
router.get('/proxy/:id/config', ProxyController.getConfig);
router.post('/proxy/:id/config', ProxyController.saveConfig);

// Atalhos para ativar/desativar
router.post('/proxy/:id/enable', ProxyController.enable);
router.post('/proxy/:id/disable', ProxyController.disable);

/**
 * Rotas de Gerenciamento de CDN
 */
router.get('/cdn/list', CdnController.list);
router.post('/cdn/folder', CdnController.createFolder);
router.post('/cdn/upload', CdnService.upload.single('file'), CdnController.uploadFile);
router.delete('/cdn/item', CdnController.deleteItem);

// Rota para upload em partes (Chunked Upload)
router.post('/cdn/upload/chunk', CdnService.upload.single('chunk'), ProxyController.uploadChunk);

module.exports = router;
