import { defineConfig } from 'vite';
import react, { reactCompilerPreset } from '@vitejs/plugin-react';
import babel from '@rolldown/plugin-babel';

const apiTarget = 'http://192.168.100.116:5026';

// https://vite.dev/config/
export default defineConfig({
    plugins: [
        react(),
        babel({ presets: [reactCompilerPreset()] })
    ],
    server: {
        proxy: {
            '/api': {
                target: apiTarget,
                changeOrigin: true,
            },
            '/uploads': {
                target: apiTarget,
                changeOrigin: true,
            },
            '/hubs': {
                target: apiTarget,
                changeOrigin: true,
                ws: true,
            },
        },
    },
    test: {
        environment: 'jsdom',
        setupFiles: './src/test/setup.js',
    },
});
