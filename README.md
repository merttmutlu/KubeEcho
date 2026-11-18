# Kubernetes deployment test service

This README describes the planned .NET Minimal API for identifying which pod handled a request and testing how Kubernetes responds to probe failures.

## Configure the service type

Set `SERVICE_TYPE` to choose the value returned in the `service` field of `GET /`. When the variable is unset, the value defaults to `KubeEcho`.

To run locally as a frontend, start the app from the project directory.

```sh
SERVICE_TYPE=frontend dotnet run --launch-profile http
```

From another terminal, query the root endpoint. The hostname in the response identifies the machine running the app.

```sh
curl http://localhost:5166/
# {"service":"frontend","hostname":"192"}
```

In Kubernetes, add the following `env` entry to the KubeEcho container under `spec.template.spec.containers` in each Deployment. Set the value to `frontend`, `backend`, or `database` to identify that deployment using the same image.

```yaml
env:
  - name: SERVICE_TYPE
    value: "frontend"
```

## Endpoints

| Endpoint | Behavior |
| --- | --- |
| `GET /` | Returns `200 OK` with the service name and pod hostname. Curl the Service address to see which pod answered each request. |
| `GET /health/ready` | Returns `200 OK` with `ready` when the readiness flag is true, or `503 Service Unavailable` with `not ready` when false. |
| `GET /health/live` | Returns `200 OK` with `live` when the liveness flag is true, or `503 Service Unavailable` with `not live` when false. |
| `PATCH /health` | Accepts a JSON object with a boolean `ready`, `live`, or both. Updates the supplied flags on the pod handling the request and returns `200 OK` with both current values. |

Each pod keeps its flags in memory. Both start as `true` whenever the application starts. For example, sending `{"ready":false}` to a healthy pod returns `{"ready":false,"live":true}`.

## Test a single pod with curl

Point the Kubernetes readiness probe at `/health/ready` and the liveness probe at `/health/live`. Port-forward directly to the pod so every command reaches the same replica, even after it becomes unready. The examples assume the forward listens on local port 8080.

```sh
base=http://localhost:8080
curl "$base/"

# Fail readiness and check both probes.
curl -X PATCH "$base/health" -H 'Content-Type: application/json' -d '{"ready":false}'
curl -i "$base/health/ready"  # 503
curl -i "$base/health/live"   # 200

# Restore readiness.
curl -X PATCH "$base/health" -H 'Content-Type: application/json' -d '{"ready":true}'
curl -i "$base/health/ready"  # 200

# Fail liveness.
curl -X PATCH "$base/health" -H 'Content-Type: application/json' -d '{"live":false}'
curl -i "$base/health/live"   # 503
```

Once the readiness probe reaches its failure threshold, Kubernetes marks the pod unready and the Service normally stops routing new traffic to it. Once the liveness probe reaches its failure threshold, Kubernetes restarts the container. Both flags then return to `true`.
