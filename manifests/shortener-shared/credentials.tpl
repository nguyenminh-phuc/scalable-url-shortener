
# kubetpl:syntax:go-template

apiVersion: v1
kind: Secret
metadata:
  name: {{ .PREFIX }}-redis
data:
  redis-password: {{ .REDIS_PASSWORD | b64enc | quote }}
---
apiVersion: v1
kind: Secret
metadata:
  name: {{ .PREFIX }}-rabbitmq
data:
  rabbitmq-password: {{ .RABBITMQ_PASSWORD | b64enc | quote }}
  rabbitmq-erlang-cookie: {{ .RABBITMQ_ERLANG_COOKIE | b64enc | quote }}
