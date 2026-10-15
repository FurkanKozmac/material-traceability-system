# Multi-stage Dockerfile for React Administration Web Portal

# --- Stage 1: Build ---
FROM node:20.19.5-alpine3.22@sha256:6178e78b972f79c335df281f4b7674a2d85071aae2af020ffa39f0a770265435 AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

# Optional build-time argument for API URL if custom domain is used
ARG VITE_API_URL=/api
ENV VITE_API_URL=$VITE_API_URL

RUN npm run build

# --- Stage 2: Serve ---
FROM nginx:1.29.1-alpine3.22@sha256:42a516af16b852e33b7682d5ef8acbd5d13fe08fecadc7ed98605ba5e3b26ab8 AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
