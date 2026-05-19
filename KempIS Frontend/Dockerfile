ARG NODE_VERSION=24.15.0
ARG CADDY_VERSION=2.11.2

FROM node:${NODE_VERSION}-alpine AS builder
WORKDIR /app

COPY package*.json ./

RUN npm ci

COPY . .
RUN npm run build --configuration=production

FROM caddy:${CADDY_VERSION}

COPY --from=builder /app/dist/kempis-fe/browser/cs /usr/share/caddy
COPY --from=builder /app/dist/kempis-fe/browser/en /usr/share/caddy/en
COPY Caddyfile /etc/caddy/Caddyfile

EXPOSE 80/tcp

HEALTHCHECK           \
  --interval=10s      \
  --timeout=2s        \
  --start-period=10s  \
  --retries=3         \
    CMD curl -f http://localhost:80/ || exit 1
