{{/*
Expand the name of the chart.
*/}}
{{- define "shortener-admin.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "shortener-admin.fullname" -}}
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
{{- define "shortener-admin.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "shortener-admin.labels" -}}
helm.sh/chart: {{ include "shortener-admin.chart" . }}
{{ include "shortener-admin.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{- define "shortener-admin.metricLabels" -}}
{{ include "shortener-admin.labels" . }}
shortener/service-type: metrics
{{- end }}

{{/*
Selector labels
*/}}
{{- define "shortener-admin.selectorLabels" -}}
app.kubernetes.io/name: {{ include "shortener-admin.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{- define "shortener-admin.selectorMetricLabels" -}}
{{ include "shortener-admin.selectorLabels" . }}
shortener/service-type: metrics
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "shortener-admin.serviceAccountName" -}}
{{- include "shortener-admin.fullname" . }}
{{- end }}

{{- define "shortener-admin.isRelease" -}}
{{- if .Values.release }}
{{- true }}
{{- end }}
{{- end }}

{{- define "shortener-admin.checkSecret" -}}
{{- $name := .name }}
{{- $namespace := .namespace }}
echo "Checking {{ $name }} secret..."
if ! kubectl get secret {{ $name }} -n {{ $namespace }}; then
  echo "Secret {{ $name }} not found."
  exit 1
fi
{{- end }}

{{- define "shortener-admin.checkReady" -}}
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

{{- define "shortener-admin.getReplicasAndCheckReady" -}}
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

{{- define "shortener-admin.waitForDependencies" -}}
- name: wait-for-dependencies
  image: {{ .Values.admin.jobs.kubectl }}
{{- if .Values.admin.jobs.useIstio }}
  securityContext:
    runAsUser: 1337
{{- end }}
  command: [ "/bin/sh", "-c" ]
  args:
    - |
{{ include "shortener-admin.getReplicasAndCheckReady" (dict
  "name" .Values.zookeeper.statefulSetName
  "type" "statefulset"
  "namespace" .Release.Namespace) | indent 6 }}
{{ include "shortener-admin.getReplicasAndCheckReady" (dict
  "name" .Values.rabbitmq.statefulSetName
  "type" "statefulset"
  "namespace" .Release.Namespace) | indent 6 }}
{{ include "shortener-admin.getReplicasAndCheckReady" (dict
  "name" .Values.redis.statefulSetName
  "type" "statefulset"
  "namespace" .Release.Namespace) | indent 6 }}
{{- if (include "shortener-admin.isRelease" .) }}
{{ $postgresql_ha := index .Values "postgresql-ha" }}
{{ include "shortener-admin.checkReady" (dict
  "name" (printf "%s-postgresql-ha-postgresql" (include "shortener-admin.fullname" .))
  "type" "statefulset"
  "replicas" $postgresql_ha.postgresql.replicaCount
  "namespace" .Release.Namespace) | indent 6 }}
{{ include "shortener-admin.checkReady" (dict
  "name" (printf "%s-postgresql-ha-pgpool" (include "shortener-admin.fullname" .))
  "type" "deployment"
  "replicas" $postgresql_ha.pgpool.replicaCount
  "namespace" .Release.Namespace) | indent 6 }}
{{- else }}
{{ include "shortener-admin.checkReady" (dict
  "name" (printf "%s-postgresql" (include "shortener-admin.fullname" .))
  "type" "statefulset"
  "replicas" 1
  "namespace" .Release.Namespace) | indent 6 }}
{{- end }}
      echo "Done."
{{- end }}

{{- define "shortener-admin.useIstio" -}}
{{- if .Values.admin.jobs.useIstio }}
trap "curl --max-time 2 -s -f -XPOST http://127.0.0.1:15020/quitquitquit" EXIT
while ! curl -s -f http://127.0.0.1:15020/healthz/ready; do sleep 1; done
echo "Ready!"
{{- end }}
{{- end }}
