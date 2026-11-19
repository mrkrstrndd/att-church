# Church Attendance Web

Angular 20 standalone application for the Church Attendance System. The root [README](../README.md) covers full setup, Docker, authentication, and testing.

## Development

```bash
npm install
npm start
```

Open <http://localhost:4200>. `proxy.conf.json` forwards `/api` to the ASP.NET Core API at `http://localhost:5180`.

## Production build

```bash
npm run build
```

The output is `dist/church-attendance-web/browser`. The included `Dockerfile` builds this output and serves it with nginx, proxying `/api` to the `api` service.

## Tests

```bash
npm test -- --watch=false --browsers=ChromeHeadless
```
