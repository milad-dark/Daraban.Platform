# Frontend Dockerfile
FROM node:22-alpine AS build
WORKDIR /app
#
COPY package.json package-lock.json* ./
RUN npm ci
#
COPY . .
RUN npm run build:prod
#
FROM nginx:1.27-alpine AS production
#
RUN rm /etc/nginx/conf.d/default.conf
#
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
#
COPY --from=build /app/dist/daraban-frontend/browser /usr/share/nginx/html
EXPOSE 80
