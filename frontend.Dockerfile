FROM node:24.16-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY angular.json .postcssrc.json tsconfig.app.json tsconfig.json ./
COPY public/ public/
COPY src/ src/
RUN npm run build

FROM nginx:1.28-alpine AS final
COPY docker/nginx/nginx.conf /etc/nginx/nginx.conf
COPY docker/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/astronomy-picture-explorer/browser/ /usr/share/nginx/html/

RUN addgroup -S astronomy && adduser -S astronomy -G astronomy \
    && chown -R astronomy:astronomy /var/cache/nginx /var/run /var/log/nginx /usr/share/nginx/html

USER astronomy
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=10s --retries=6 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
