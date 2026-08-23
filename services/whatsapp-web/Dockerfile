FROM node:22-alpine

WORKDIR /app

COPY package*.json ./
RUN npm install --include=dev

COPY . .

EXPOSE 10000

CMD ["sh", "-c", "PORT=${PORT:-10000} node server.mjs"]
