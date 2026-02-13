# Change domain after installation

This guide updates the domain for an existing Outline SS Management install, obtains a new SSL certificate, and restarts services.

## Prerequisites

- DNS A record for the new domain points to the server public IP.
- You can run commands with sudo on the host.
- The install directory is known (default: /opt/outline-manager).

## Steps

1) Stop the running containers to free ports 80/443

```bash
cd /opt/outline-manager
sudo docker compose stop nginx
```

2) Issue a new certificate for the new domain

```bash
sudo certbot certonly --standalone \
  -d NEW_DOMAIN \
  -m admin@NEW_DOMAIN \
  --agree-tos \
  --noninteractive
```

Notes:
- Replace NEW_DOMAIN and the email address.
- If you previously used a different email, you can reuse it.

3) Update the environment file with the new domain

```bash
cd /opt/outline-manager
sudo sed -i "s/^DOMAIN=.*/DOMAIN=NEW_DOMAIN/" .env
```

4) Restart services to apply the change

```bash
sudo docker compose up -d
```

5) Verify

```bash
sudo docker compose logs -f nginx
```

You should see Nginx start without errors and serve the new domain.

## Optional cleanup

If you no longer need the old certificate, you can remove it:

```bash
sudo certbot delete --cert-name OLD_DOMAIN
```

## Troubleshooting

- If certbot fails with a port 80 error, make sure no other service is listening on 80/443.
- If Nginx reports a missing cert, confirm the new cert exists at:
  /etc/letsencrypt/live/NEW_DOMAIN/fullchain.pem
  /etc/letsencrypt/live/NEW_DOMAIN/privkey.pem
- If DNS is not updated yet, certbot will fail the HTTP-01 challenge.
