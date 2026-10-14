FROM node:22-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev
COPY src ./src
RUN mkdir /data && chown node:node /data
USER node
ENV PORT=8787 DATA_DIR=/data
EXPOSE 8787
VOLUME ["/data"]
CMD ["node", "src/node.js"]
