# The bridge runs as its own small container so the .NET API image stays free of
# Node and the Claude CLI. Credentials arrive at runtime as a bind mount; nothing
# sensitive is ever baked into this image.
FROM node:22-alpine

RUN apk add --no-cache git ripgrep && \
    npm install -g @anthropic-ai/claude-code && \
    npm cache clean --force

WORKDIR /app
COPY server.js ./

# Claude Code writes session state, history and refreshed tokens, so it needs a
# writable home. The compose file mounts the host's config over this path.
ENV HOME=/home/claude
RUN mkdir -p /home/claude /work && chmod 777 /home/claude /work

ENV BRIDGE_HOST=0.0.0.0 \
    BRIDGE_PORT=8787 \
    BRIDGE_WORKDIR=/work

EXPOSE 8787
CMD ["node", "/app/server.js"]
