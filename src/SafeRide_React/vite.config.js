import { defineConfig } from 'vite';
import react, { reactCompilerPreset } from '@vitejs/plugin-react';
import babel from '@rolldown/plugin-babel';

const apiTarget1 = 'http://demotripshare.runasp.net';
const apiTarget = 'https://saferidefpt.runasp.net';

// https://vite.dev/config/
export default defineConfig({
    plugins: [
        react(),
        babel({ presets: [reactCompilerPreset()] })
    ],
    server: {
        proxy: {
            '/api': apiTarget,
            '/uploads': apiTarget,
        },
    },
});
