{{/*
Expand the name of the chart.
*/}}
{{- define "shortener-shared.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "shortener-shared.fullname" -}}
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
{{- define "shortener-shared.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "shortener-shared.labels" -}}
helm.sh/chart: {{ include "shortener-shared.chart" . }}
{{ include "shortener-shared.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "shortener-shared.selectorLabels" -}}
app.kubernetes.io/name: {{ include "shortener-shared.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "shortener-shared.serviceAccountName" -}}
{{- include "shortener-shared.fullname" . }}
{{- end }}

{{- define "shortener-shared.isRelease" -}}
{{- if .Values.release }}
{{- true }}
{{- end }}
{{- end }}

{{- define "shortener-shared.checkSecret" -}}
{{- $name := .name }}
{{- $namespace := .namespace }}
echo "Checking {{ $name }} secret..."
if ! kubectl get secret {{ $name }} -n {{ $namespace }}; then
  echo "Secret {{ $name }} not found."
  exit 1
fi
{{- end }}

{{- define "shortener-shared.useIstio" -}}
{{- if .Values.shared.jobs.useIstio }}
trap "curl --max-time 2 -s -f -XPOST http://127.0.0.1:15020/quitquitquit" EXIT
while ! curl -s -f http://127.0.0.1:15020/healthz/ready; do sleep 1; done
echo "Ready!"
{{- end }}
{{- end }}
