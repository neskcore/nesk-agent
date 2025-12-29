const express = require('express');
const router = express.Router();
const ProxyController = require('./controllers/proxyController');
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

// Atalhos para ativar/desativar
router.post('/proxy/:id/enable', ProxyController.enable);
router.post('/proxy/:id/disable', ProxyController.disable);

module.exports = router;