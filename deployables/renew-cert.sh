#!/bin/bash

# Renew SSL Certificate Script
# The following code is a template for renewing SSL certificates using certbot in a Docker container.

# # Run certbot and capture output
# OUTPUT=$(docker run --rm \
#   -v /root/cert-data/etc/letsencrypt:/etc/letsencrypt \
#   -v /root/cert-data/var/lib/letsencrypt:/var/lib/letsencrypt \
#   -v /root/cert-data/var/log/letsencrypt:/var/log/letsencrypt \
#   -v /root/cert-data/webroot:/var/www/html \
#   certbot/certbot certonly \
#     --webroot -w /var/www/html \
#     -d private.mayeli.uk \
#     --agree-tos \
#     --email admin@mayeli.uk \
#     --non-interactive 2>&1)

# # Capture the exit code immediately
# EXIT_CODE=$?

# # Print certbot output
# echo "$OUTPUT"

# # Check if certbot succeeded
# if [ $EXIT_CODE -eq 0 ]; then
#     # Extract the folder name from the output
#     FOLDER_NAME=$(echo "$OUTPUT" | grep -oP 'Certificate is saved at: /etc/letsencrypt/live/\K[^/]+' | head -1)
    
#     if [ -z "$FOLDER_NAME" ]; then
#         echo "Error: Could not extract folder name from certbot output"
#         exit 1
#     fi
    
#     echo "Using certificate folder: $FOLDER_NAME"
    
#     # Copy certificates using the extracted folder name
#     # Use fullchain.pem which contains both server cert and intermediate cert
#     # This is needed for OCSP stapling to work properly
#     cp /root/cert-data/etc/letsencrypt/live/${FOLDER_NAME}/fullchain.pem /root/data/ssl-wc/the.pem && \
#     cp /root/cert-data/etc/letsencrypt/live/${FOLDER_NAME}/privkey.pem /root/data/ssl-wc/the.key && \
#     echo "Certificates copied successfully" && \
#     docker restart reverse-proxy && \
#     echo "Nginx restarted successfully"
# else
#     echo "Error: Certbot failed to obtain certificate"
#     exit 1
# fi
