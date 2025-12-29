FROM nginx:alpine

# Copy built files to nginx
COPY dist/ /usr/share/nginx/html/

# Copy nginx config for SPA routing
RUN echo 'server { \
    listen 8080; \
    root /usr/share/nginx/html; \
    index index.html; \
    location / { \
        try_files $uri $uri/ /index.html; \
    } \
}' > /etc/nginx/conf.d/default.conf

# Copy config generator script as nginx entrypoint.d script
COPY docker-entrypoint.sh /docker-entrypoint.d/40-generate-config.sh
RUN chmod +x /docker-entrypoint.d/40-generate-config.sh

EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
