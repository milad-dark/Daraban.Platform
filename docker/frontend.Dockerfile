# =============================================================================
# Daraban Frontend (Angular) � used by CI as a build-validation target too.
#   dev:        ng serve with hot reload (docker-compose.override.yml)
#   production: static bundle served by nginx
# =============================================================================
FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
# ngx-echarts@22 carries a @angular/core>=22 peer while the app is on 21.x;
# the committed lock predates strict peer checks, so install it as-is.
RUN npm ci --legacy-peer-deps

FROM deps AS build
COPY . .
RUN npm run build:prod

# Dev server target (compose override). Sources are bind-mounted over /app at runtime.
FROM deps AS dev
COPY . .
EXPOSE 4200
CMD ["npx", "ng", "serve", "--host", "0.0.0.0", "--port", "4200"]

FROM nginx:1.27-alpine AS production

RUN rm /etc/nginx/conf.d/default.conf

RUN echo 'server { \
    listen 80; \
    root /usr/share/nginx/html; \
    index index.html; \
    location / { \
        try_files $uri $uri/ /index.html; \
    } \
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ { \
        expires 1y; \
        add_header Cache-Control "public, immutable"; \
    } \
}' > /etc/nginx/conf.d/default.conf

COPY --from=build /app/dist/daraban-frontend/browser /usr/share/nginx/html
EXPOSE 80