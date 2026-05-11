# server/monitoring/

Prometheus + Grafana + Alertmanager 스택 설정 파일 모음. `server/docker-compose.monitoring.yml` 가 이 디렉토리를 컨테이너에 read-only 로 마운트한다.

운영 절차 / 검증 / 트러블슈팅은 [server/README.md `## 모니터링 / 임계 알림`](../README.md#모니터링--임계-알림-prometheus--grafana--alertmanager) 참고 — 본 문서는 디렉토리 구조와 각 파일의 책임만 짧게 정리한다.

## 구조

```
monitoring/
├── prometheus/
│   ├── prometheus.yml              # scrape + alerting 설정
│   └── rules/
│       └── server-health.yml       # CPU/Mem/Disk > 80% alert rules (INFRA-21)
├── alertmanager/
│   └── alertmanager.yml            # Mattermost (slack_configs) receiver + 24h repeat
└── grafana/
    ├── provisioning/
    │   ├── datasources/
    │   │   └── prometheus.yml      # Prometheus datasource 자동 등록 (uid: prometheus)
    │   └── dashboards/
    │       └── dashboards.yml      # /etc/grafana/dashboards 디렉토리 자동 스캔 설정
    └── dashboards/
        ├── node-exporter-full.json # Grafana.com community ID 1860 (호스트 종합)
        └── lostmemory-containers.json   # 자체 작성 (컨테이너 CPU/Mem/Network)
```

## 책임 / 의존

| 파일 | 책임 | 변경 시 영향 |
|---|---|---|
| `prometheus/prometheus.yml` | scrape target / rule 파일 경로 / alertmanager 주소 | scrape target 추가 / 제거 시 hotreload 가능 (`/-/reload`) |
| `prometheus/rules/server-health.yml` | 임계 알림 expr / for / 라벨 / annotation | 임계 조정 / 새 rule 추가는 hotreload 가능 |
| `alertmanager/alertmanager.yml` | 알림 routing / grouping / repeat_interval / receiver 템플릿 | 변경 시 `wget -qO- --post-data='' http://alertmanager:9093/-/reload` |
| `grafana/provisioning/datasources/prometheus.yml` | Prometheus datasource UID `prometheus` 고정 | dashboards/lostmemory-containers.json 가 같은 UID 참조 — 변경 시 둘 다 갱신 |
| `grafana/provisioning/dashboards/dashboards.yml` | 파일 기반 dashboards provider (30s 마다 폴링) | `updateIntervalSeconds` 조정 가능. `allowUiUpdates: true` 라 UI 임시 수정도 허용 |
| `grafana/dashboards/*.json` | 시각화 대시보드 정의 | provider 의 폴링 주기로 자동 반영 (컨테이너 재기동 불필요) |

## 외부 의존성

- `MATTERMOST_WEBHOOK_URL` (`.env`) — alertmanager entrypoint 가 env → 파일로 작성 후 `api_url_file` 참조.
- `GRAFANA_ADMIN_PASSWORD` (`.env`) — Grafana admin 계정. 미설정 시 컨테이너 startup fail.
- `DOMAIN` (`.env`) — Grafana 의 `GF_SERVER_ROOT_URL` 에 사용 (`https://${DOMAIN}/grafana/`).
- 같은 compose project 의 `server_frontend` 네트워크 (nginx ↔ grafana) — `docker-compose.monitoring.yml` 이 `external: true` 로 참조.

## 핫리로드 cheatsheet

```bash
cd /home/ubuntu/lostmemory/server

# prometheus.yml / rules/*.yml 변경 후
docker compose -f docker-compose.monitoring.yml --env-file .env exec prometheus \
  wget -qO- --post-data='' http://localhost:9090/-/reload

# alertmanager.yml 변경 후
docker compose -f docker-compose.monitoring.yml --env-file .env exec alertmanager \
  wget -qO- --post-data='' http://localhost:9093/-/reload

# datasource provisioning / dashboard provisioning yaml 변경 후 (grafana 재기동 필요)
docker compose -f docker-compose.monitoring.yml --env-file .env up -d --force-recreate grafana
```

## 추가 대시보드 import

운영자가 Grafana UI 에서 `Dashboards → New → Import → Grafana.com ID` 로 임시 import 후 마음에 들면 JSON export → 본 디렉토리 `dashboards/` 에 commit 해서 영구 자동 import. 주의: community 대시보드는 `${DS_PROMETHEUS}` 같은 `__inputs` 변수를 사용하는 경우가 많아 export 시 datasource UID 를 `prometheus` 로 명시 치환 후 commit.
