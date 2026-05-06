# Hostinger Ubuntu Deployment

This guide deploys the Advait Services RCS wrapper on a Hostinger Ubuntu VPS with SQL Server, Docker Compose, and Nginx.

## Assumptions

- Hostinger plan is a VPS with SSH and `sudo`.
- Your domain points to the VPS public IP.
- Ports `80` and `443` are open in Hostinger/firewall settings.
- You deploy from the repository root.

## 1. Install Server Dependencies

```bash
sudo apt update
sudo apt install -y ca-certificates curl gnupg nginx
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"
```

Log out and SSH back in so Docker group membership applies.

Install .NET SDK for migrations:

```bash
sudo apt update
sudo apt install -y dotnet-sdk-9.0
```

If Ubuntu cannot find `dotnet-sdk-9.0`, install it using Microsoft package feed for your Ubuntu version, then rerun the command.

## 2. Upload Project

```bash
sudo mkdir -p /opt/advait-services
sudo chown -R "$USER":"$USER" /opt/advait-services
rsync -av --delete ./ /opt/advait-services/
cd /opt/advait-services
```

## 3. Configure Secrets

```bash
cp .env.production.example .env.production
nano .env.production
```

Set strong values:

- `SQL_SA_PASSWORD`: strong SQL Server SA password.
- `JWT_SIGNING_KEY`: at least 32 characters, random.
- `JIOCX_BASE_URL`: keep UAT URL unless JioCX gives production URL.

Never commit `.env.production`.

## 4. Deploy App and Database

```bash
chmod +x deploy/scripts/deploy-ubuntu.sh
APP_DIR=/opt/advait-services ./deploy/scripts/deploy-ubuntu.sh
```

The script:

- Builds the MVC Docker image.
- Starts SQL Server.
- Runs EF Core migrations against SQL Server.
- Starts the web app on `127.0.0.1:5080`.
- Binds SQL Server to `127.0.0.1:1433` for local migrations only.

Check health:

```bash
curl http://127.0.0.1:5080/health
docker compose --env-file .env.production -f docker-compose.prod.yml logs -f web
```

## 5. Configure Nginx

```bash
sudo cp deploy/nginx/advait-services.conf /etc/nginx/sites-available/advait-services
sudo nano /etc/nginx/sites-available/advait-services
```

Replace `your-domain.com` with your real domain.

```bash
sudo ln -sf /etc/nginx/sites-available/advait-services /etc/nginx/sites-enabled/advait-services
sudo nginx -t
sudo systemctl reload nginx
```

## 6. Enable HTTPS

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d your-domain.com -d www.your-domain.com
```

## 7. First Login

Seeded admin:

- Email: `admin@local.test`
- Password: `ChangeMe@12345`

Change this password immediately after first login.

## 8. Operational Notes

- Uploaded logos/media are stored in Docker volume `web-uploads`.
- SQL database files are stored in Docker volume `sqlserver-data`.
- Data Protection keys are stored in Docker volume `dataprotection-keys`.
- Back up all three volumes.
- Webhook URL should be:

```text
https://your-domain.com/Webhooks/JioCx
```

## Useful Commands

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml ps
docker compose --env-file .env.production -f docker-compose.prod.yml logs -f web
docker compose --env-file .env.production -f docker-compose.prod.yml restart web
docker compose --env-file .env.production -f docker-compose.prod.yml down
```

To apply new migrations after code changes:

```bash
APP_DIR=/opt/advait-services ./deploy/scripts/deploy-ubuntu.sh
```
