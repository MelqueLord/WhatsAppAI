FROM node:22-alpine

RUN apk add --no-cache python3 make g++ vips-dev

WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

EXPOSE 10000

CMD ["sh", "-c", "PORT=${PORT:-10000} node server.mjs"]
