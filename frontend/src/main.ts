import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { configurarExpiracion } from './api'
import './estilos/base.css'

const app = createApp(App)
app.use(createPinia())
app.use(router)
configurarExpiracion(() => {
  if (!router.currentRoute.value.meta.publica) router.push({ path: '/login', query: { expirada: '1' } })
})
app.mount('#app')
