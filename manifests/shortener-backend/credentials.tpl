
# kubetpl:syntax:go-template

{{- if eq (int .SHARD_ID) 0 }}
apiVersion: v1
kind: Secret
metadata:
  name: {{ .PREFIX }}-jwt
data:
  issuer: {{ .JWT_ISSUER | b64enc | quote }}
  audience: {{ .JWT_AUDIENCE | b64enc | quote }}
  key: {{ .JWT_SECRET_KEY | b64enc | quote }}
{{- end }}
---
apiVersion: v1
kind: Secret
metadata:
  name: {{ .PREFIX }}{{ .SHARD_ID }}-postgresql
data:
  password: {{ .POSTGRES_PASSWORD | b64enc | quote }}
  postgres-password: {{ .POSTGRES_PASSWORD | b64enc | quote }}
  repmgr-password: {{ .REPMGR_PASSWORD | b64enc | quote }}
  admin-password: {{ .PGPOOL_PASSWORD | b64enc | quote }}
