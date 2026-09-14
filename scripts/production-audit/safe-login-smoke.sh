#!/usr/bin/env bash
# Safe HTTP login smoke test across 17 branch APIs.
# Classes: MALFORMED_PAYLOAD | AUTH_REJECT | HTTP_200 | CONNECTION_FAILURE | SERVER_ERROR
# Never prints passwords or JWT.
set -euo pipefail
umask 077
TMP=$(mktemp)
trap 'rm -f "$TMP"' EXIT

TEST_USERNAME="${TEST_USERNAME:?}"
TEST_PASSWORD="${TEST_PASSWORD:?}"
TEST_ROLE="${TEST_ROLE:?}"

if [[ "$TEST_ROLE" == "محاسب رئيسي" || "$TEST_ROLE" == "مدير فرع" ]]; then
  ENDPOINT="Users/Users_LoginAdmin"
else
  ENDPOINT="Users/Users_LoginEmployee"
fi

python3 -c 'import json,os; print(json.dumps({"userName":os.environ["TEST_USERNAME"],"password":os.environ["TEST_PASSWORD"]},ensure_ascii=False))' >"$TMP"
python3 -c 'import json,sys; json.load(open(sys.argv[1],encoding="utf-8")); print("JSON_OK")' "$TMP"

HOSTS=(
  "Dewania|http://sharenewdewania.alsaaeidy.com/api"
  "Karbala|http://sharenewkarbala.alsaaeidy.com/api"
  "Kot|http://sharenewkot.alsaaeidy.com/api"
  "Maysan|http://sharenewmaysanl.alsaaeidy.com/api"
  "Muthanna|http://sharenewmothana.alsaaeidy.com/api"
  "Mosul|http://sharenewmusol.alsaaeidy.com/api"
  "Najaf|http://sharenewnajaf.alsaaeidy.com/api"
  "Nasiriyah|http://sharenewnasria.alsaaeidy.com/api"
  "Babil|http://sharenewrbabil.alsaaeidy.com/api"
  "Basra|http://sharenewrbasra.alsaaeidy.com/api"
  "BasraAlanwar|http://sharenewrbasranwar.alsaaeidy.com/api"
  "Diyala|http://sharenewrdeiala.alsaaeidy.com/api"
  "Karkh|http://sharenewrkarak.alsaaeidy.com/api"
  "Kirkuk|http://sharenewrkarkok.alsaaeidy.com/api"
  "Rusafa|http://sharenewrosafa.alsaaeidy.com/api"
  "KarakAqeel|http://shortnewkarakaqeel.alsaaeidy.com/api"
  "RusafaAqeel|http://shortnewrosafaaqeel.alsaaeidy.com/api"
)

printf 'Branch\tRole\tHTTP\tClass\tPASS_FAIL\n'
for entry in "${HOSTS[@]}"; do
  name="${entry%%|*}"
  base="${entry##*|}"
  url="${base}/${ENDPOINT}"
  body_file=$(mktemp)
  set +e
  code=$(curl -sS -o "$body_file" -w "%{http_code}" -X POST "$url" \
    -H "Content-Type: application/json" \
    --data-binary @"$TMP" \
    --connect-timeout 15 --max-time 30)
  curl_ec=$?
  set -e
  if [[ $curl_ec -ne 0 ]]; then
    printf '%s\t%s\t000\tCONNECTION_FAILURE\tFAIL\n' "$name" "$TEST_ROLE"
    rm -f "$body_file"
    continue
  fi
  class="OTHER"
  pf="FAIL"
  if [[ "$code" == "200" ]]; then
    if python3 -c 'import json,sys; d=json.load(open(sys.argv[1],encoding="utf-8")); raise SystemExit(0 if (d.get("token") or d.get("Token")) else 1)' "$body_file"; then
      class="HTTP_200"; pf="PASS"
    else
      class="SERVER_ERROR"
    fi
  elif [[ "$code" =~ ^5 ]]; then
    class="SERVER_ERROR"
  elif [[ "$code" == "400" || "$code" == "401" ]]; then
    msg=$(python3 -c 'import json,sys
try:
 d=json.load(open(sys.argv[1],encoding="utf-8")); print(d.get("message") or "")
except Exception:
 print("")' "$body_file")
    if [[ "$msg" == *"غير صحيحة"* ]]; then class="AUTH_REJECT"; else class="MALFORMED_PAYLOAD"; fi
  fi
  printf '%s\t%s\t%s\t%s\t%s\n' "$name" "$TEST_ROLE" "$code" "$class" "$pf"
  rm -f "$body_file"
done
