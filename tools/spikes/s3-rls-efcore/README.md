# s3-rls-efcore

Estado: incorporado al producto.

La puerta (sin fuga de `app.customer_id` entre transacciones del pool) queda
cubierta por el producto: `SET LOCAL` vía `set_config(..., true)` al inicio de
cada transacción de logística, políticas `FORCE ROW LEVEL SECURITY`, y
`SecurityGapTests.Portal_Customers_Cannot_Read_Each_Others_Sales_Orders`
(dos clientes por API y un `SELECT` directo). No hace falta un prototipo
aparte en esta carpeta.

Ver criterios en docs/es/spikes/README.md.
