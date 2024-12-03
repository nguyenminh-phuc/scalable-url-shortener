{{/*
Expand the name of the chart.
*/}}
{{- define "shortener-backend.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "shortener-backend.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "shortener-backend.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "shortener-backend.labels" -}}
helm.sh/chart: {{ include "shortener-backend.chart" . }}
{{ include "shortener-backend.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{- define "shortener-backend.metricLabels" -}}
{{ include "shortener-backend.labels" . }}
shortener/service-type: metrics
{{- end }}

{{/*
Selector labels
*/}}
{{- define "shortener-backend.selectorLabels" -}}
app.kubernetes.io/name: {{ include "shortener-backend.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{- define "shortener-backend.selectorMetricLabels" -}}
{{ include "shortener-backend.selectorLabels" . }}
shortener/service-type: metrics
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "shortener-backend.serviceAccountName" -}}
{{- include "shortener-backend.fullname" . }}
{{- end }}

{{- define "shortener-backend.isRelease" -}}
{{- if .Values.release }}
{{- true }}
{{- end }}
{{- end }}

{{- define "shortener-backend.checkSecret" -}}
{{- $name := .name }}
{{- $namespace := .namespace }}
echo "Checking {{ $name }} secret..."
if ! kubectl get secret {{ $name }} -n {{ $namespace }}; then
  echo "Secret {{ $name }} not found."
  exit 1
fi
{{- end }}

{{- define "shortener-backend.checkReady" -}}
{{- $name := .name }}
{{- $type := .type }}
{{- $replicas := .replicas }}
{{- $namespace := .namespace }}
echo "Checking {{ $type }}/{{ $name }}..."
kubectl wait --for=jsonpath='{.status.readyReplicas}'={{ $replicas }} {{ $type }}/{{ $name }} -n {{ $namespace }} --timeout=5m
if [ $? -ne 0 ]; then
  exit 1
fi
{{- end }}

{{- define "shortener-backend.getReplicasAndCheckReady" -}}
{{- $name := .name }}
{{- $type := .type }}
{{- $namespace := .namespace }}
echo "Getting {{ $type }}/{{ $name }} replicas..."
REPLICAS=$(kubectl get -o=jsonpath='{.status.replicas}' {{ $type }}/{{ $name }} -n {{ $namespace }})
echo "Checking {{ $type }}/{{ $name }}..."
kubectl wait --for=jsonpath='{.status.readyReplicas}'=$REPLICAS {{ $type }}/{{ $name }} -n {{ $namespace }} --timeout=5m
if [ $? -ne 0 ]; then
  exit 1
fi
{{- end }}

{{- define "shortener-backend.waitForDependencies" -}}
- name: wait-for-dependencies
  image: {{ .Values.backend.jobs.kubectl }}
{{- if .Values.backend.jobs.useIstio }}
  securityContext:
    runAsUser: 1337
{{- end }}
  command: [ "/bin/sh", "-c" ]
  args:
    - |
{{ include "shortener-backend.getReplicasAndCheckReady" (dict
  "name" .Values.zookeeper.statefulSetName
  "type" "statefulset"
  "namespace" .Release.Namespace) | indent 6 }}
{{ include "shortener-backend.getReplicasAndCheckReady" (dict
  "name" .Values.rabbitmq.statefulSetName
  "type" "statefulset"
  "namespace" .Release.Namespace) | indent 6 }}
{{ include "shortener-backend.getReplicasAndCheckReady" (dict
  "name" .Values.redis.statefulSetName
  "type" "statefulset"
  "namespace" .Release.Namespace) | indent 6 }}
{{ include "shortener-backend.getReplicasAndCheckReady" (dict
  "name" .Values.admin.deploymentName
  "type" "deployment"
  "namespace" .Release.Namespace) | indent 6 }}
{{- if (include "shortener-backend.isRelease" .) }}
{{ $postgresql_ha := index .Values "postgresql-ha" }}
{{ include "shortener-backend.checkReady" (dict
  "name" (printf "%s-postgresql-ha-postgresql" (include "shortener-backend.fullname" .))
  "type" "statefulset"
  "replicas" $postgresql_ha.postgresql.replicaCount
  "namespace" .Release.Namespace) | indent 6 }}
{{ include "shortener-backend.checkReady" (dict
  "name" (printf "%s-postgresql-ha-pgpool" (include "shortener-backend.fullname" .))
  "type" "deployment"
  "replicas" $postgresql_ha.pgpool.replicaCount
  "namespace" .Release.Namespace) | indent 6 }}
{{- else }}
{{ include "shortener-backend.checkReady" (dict
  "name" (printf "%s-postgresql" (include "shortener-backend.fullname" .))
  "type" "statefulset"
  "replicas" 1
  "namespace" .Release.Namespace) | indent 6 }}
{{- end }}
      echo "Done."
{{- end }}

{{- define "shortener-backend.useIstio" -}}
{{- if .Values.backend.jobs.useIstio }}
trap "curl --max-time 2 -s -f -XPOST http://127.0.0.1:15020/quitquitquit" EXIT
while ! curl -s -f http://127.0.0.1:15020/healthz/ready; do sleep 1; done
echo "Ready!"
{{- end }}
{{- end }}
