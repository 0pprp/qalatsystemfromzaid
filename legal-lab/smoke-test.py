# -*- coding: utf-8 -*-
"""End-to-end check of the legal system against the lab (run start-lab.ps1 first).

Walks the whole story through the real APIs: legal customers by list -> group a debtor's sales ->
send to the legal app -> archive -> judicial (200k) -> steps -> lawyer wallet and fees -> registers ->
ابطال -> payments (kept in القانونية, never posted to the branch) -> decisions -> execution (150k) ->
full settlement with and without the 35k penalty -> who can see what.
It writes to the lab databases only. Each run adds new debtors, so rebuild the lab with
`setup-lab.ps1 -Fresh` when you want a clean slate.

    python legal-lab/smoke-test.py
"""
import base64, hashlib, hmac, io, json, os, sys, time, urllib.error, urllib.parse, urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
SECRETS = json.load(open(os.path.join(HERE, "data", "lab-secrets.json"), encoding="utf-8-sig"))
GW = "http://127.0.0.1:5680/api"
NAJAF = "http://127.0.0.1:5611/api"
failures = []


def call(method, url, token=None, body=None, headers=None, form=None):
    data = None
    hdrs = dict(headers or {})
    if token:
        hdrs["Authorization"] = "Bearer " + token
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        hdrs["Content-Type"] = "application/json; charset=utf-8"
    if form is not None:
        boundary = "----lab" + str(int(time.time() * 1000))
        parts = []
        for key, value in form.items():
            if isinstance(value, tuple):
                name, content, ctype = value
                parts.append(("--%s\r\nContent-Disposition: form-data; name=\"%s\"; filename=\"%s\"\r\nContent-Type: %s\r\n\r\n"
                              % (boundary, key, name, ctype)).encode("utf-8") + content + b"\r\n")
            else:
                parts.append(("--%s\r\nContent-Disposition: form-data; name=\"%s\"\r\n\r\n%s\r\n" % (boundary, key, value)).encode("utf-8"))
        data = b"".join(parts) + ("--%s--\r\n" % boundary).encode("utf-8")
        hdrs["Content-Type"] = "multipart/form-data; boundary=" + boundary
    request = urllib.request.Request(url, data=data, method=method, headers=hdrs)
    try:
        with urllib.request.urlopen(request, timeout=90) as response:
            raw = response.read()
            status = response.status
    except urllib.error.HTTPError as error:
        raw = error.read()
        status = error.code
    try:
        return status, json.loads(raw.decode("utf-8")) if raw else None
    except ValueError:
        return status, raw


def check(name, condition, detail=""):
    print(("  ok   " if condition else "  FAIL ") + name + ("" if condition else "  -> " + str(detail)[:400]))
    if not condition:
        failures.append(name)


def login(user, password):
    status, body = call("POST", GW + "/Auth/LoginDirector", body={"userName": user, "password": password})
    assert status == 200 and body.get("token"), (status, body)
    return body["token"]


def q(text):
    return urllib.parse.quote(str(text), safe="")


def branch_jwt(user_type, user_id=1):
    """A branch-login token signed with the lab key, as the branch itself would issue."""
    def b64(raw):
        return base64.urlsafe_b64encode(raw).rstrip(b"=")
    header = b64(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    payload = b64(json.dumps({"UserID": str(user_id), "UserType": user_type, "UserName": "lab", "exp": int(time.time()) + 3600},
                             ensure_ascii=False).encode("utf-8"))
    signature = hmac.new(base64.b64decode(SECRETS["BranchSigningKey"]), header + b"." + payload, hashlib.sha256).digest()
    return (header + b"." + payload + b"." + b64(signature)).decode()


print("1) logins")
director = login(SECRETS["DirectorUser"], SECRETS["DirectorPassword"])
accountant = login(SECRETS["AccountantUser"], SECRETS["AccountantPassword"])
team = login(SECRETS["TeamUser"], SECRETS["TeamPassword"])
check("three roles log in", all([director, accountant, team]))
check("legal team cannot open the legal desk", call("GET", GW + "/legal-desk/overview", team)[0] == 403)
check("legal accountant cannot open the legal app", call("GET", GW + "/legal-app/board", accountant)[0] == 403)
check("director opens both", call("GET", GW + "/legal-desk/overview", director)[0] == 200
      and call("GET", GW + "/legal-app/board", director)[0] == 200)

print("2) legal customers by list")
status, cities = call("GET", GW + "/legal-desk/cities", accountant)
check("two lab branches", status == 200 and len(cities) == 2, cities)
status, lists = call("GET", GW + "/legal-desk/lists?cityValue=lab-najaf", accountant)
check("lists that hold legal customers, with counts", status == 200 and len(lists) >= 1 and all(l["legalCustomers"] > 0 for l in lists), lists)
big = max(lists, key=lambda l: l["legalCustomers"])
status, customers = call("GET", GW + "/legal-desk/customers?cityValue=lab-najaf&delegateId=%d" % big["delegateId"], accountant)
check("a list shows exactly its legal customers", status == 200 and len(customers) == big["legalCustomers"]
      and all(c["isLegal"] for c in customers), (len(customers or []), big))
free = [c for c in customers if not c["personId"] and c["remaining"] >= 400000]
first, second, third = free[0], free[1], free[2]
status, similar = call("GET", GW + "/legal-desk/customers/similar?cityValue=lab-najaf&customerId=%d" % first["customerId"], accountant)
check("similar-accounts lookup answers", status == 200 and isinstance(similar, list), similar)
status, found = call("GET", GW + "/legal-desk/search?q=" + q((first["customerName"] or "")[:8]), accountant)
check("cross-branch search finds the customer", status == 200 and any(r["customerId"] == first["customerId"] for r in found["rows"]), found)
status, babil = call("GET", GW + "/legal-desk/customers?cityValue=lab-babil", accountant)
babil_pick = next(c for c in babil if not c["personId"] and c["remaining"] >= 100000)

print("3) group sales into one debtor and send")
accounts = [{"cityValue": "lab-najaf", "customerId": first["customerId"]}, {"cityValue": "lab-babil", "customerId": babil_pick["customerId"]}]
unused_similar = [s for s in similar if not s.get("personId")]
if unused_similar:
    accounts.append({"cityValue": "lab-najaf", "customerId": unused_similar[0]["customerId"]})
status, created = call("POST", GW + "/legal-desk/persons", accountant, {"fullName": first["customerName"], "phone": first["phone"], "accounts": accounts})
check("debtor created from accounts in two branches", status == 200, created)
person = created["id"]
check("same account cannot join a second debtor",
      call("POST", GW + "/legal-desk/persons", accountant, {"fullName": "x", "accounts": accounts[:1]})[0] == 409)
status, detail = call("GET", GW + "/legal-desk/persons/%d" % person, accountant)
check("debtor detail has live balances", status == 200 and len(detail["accounts"]) == len(accounts)
      and all(a["isLive"] for a in detail["accounts"]), detail)
check("a debtor who was not sent cannot pay or get a discount",
      call("POST", GW + "/legal-desk/persons/%d/payments" % person, accountant, {"lines": [], "penaltyAmount": 0})[0] == 400
      and call("POST", GW + "/legal-desk/persons/%d/discounts" % person, accountant, {"amount": 1000})[0] == 409)
reg_before_send = call("GET", GW + "/legal-desk/registers", accountant)[1]
status, sent = call("POST", GW + "/legal-desk/persons/%d/send" % person, accountant, {"note": "smoke"})
check("sent to the legal app", status == 200 and sent["caseId"] > 0, sent)
case = sent["caseId"]
reg_after_send = call("GET", GW + "/legal-desk/registers", accountant)[1]
check("sending charges 200,000: a penalty on the debtor, and paid back to قلعة الضمان",
      call("GET", GW + "/legal-desk/persons/%d" % person, accountant)[1]["totals"]["penaltiesDue"] == 200000
      and reg_after_send["companyDebt"] == reg_before_send["companyDebt"] - 200000
      and reg_after_send["judicialCount"] == reg_before_send["judicialCount"] + 1
      and reg_after_send["cash"] == reg_before_send["cash"], (reg_before_send["companyDebt"], reg_after_send["companyDebt"]))
check("cannot send twice", call("POST", GW + "/legal-desk/persons/%d/send" % person, accountant, {})[0] == 409)

print("4) flow in the legal app")
status, recent = call("GET", GW + "/legal-app/cases?stage=Recent", team)
check("lands in الاسماء المضافة مؤخرا", status == 200 and any(c["id"] == case for c in recent), recent)
check("cannot jump straight to judicial", call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Judicial"})[0] == 409)
import re
status, lines = call("GET", GW + "/legal-app/cases/%d/archive-codes" % case, team)
check("archive codes are proposed for every sale before the move",
      status == 200 and len(lines) == len(accounts) and all(l["proposedCode"] and not l["error"] and not l["existingCode"] for l in lines), lines)


def code_ok(line):
    m = re.fullmatch(r"([A-Z]{2})(\d{2})-(\d+)-(\d+)", line["proposedCode"] or line["existingCode"] or "")
    if not m:
        return False
    branch = "NJ" if "النجف" in line["cityName"] else "BB"
    tail = m.group(4)
    slice_ = int(line["total"]) // 1000000
    return (m.group(1) == branch and m.group(2) == line["saleDate"][2:4]
            and (len(tail) == 3 if slice_ == 0 else int(tail) // 1000 == slice_))


check("code = branch + sale year + box + (total sale / 1,000,000) slice and sequence", all(code_ok(l) for l in lines), lines)
check("sales of one debtor get different codes", len({l["proposedCode"] for l in lines}) == len(lines), lines)
status, moved = call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Archive"})
check("recent -> archive issues the proposed codes", status == 200
      and sorted(moved["archiveCodes"]) == sorted(l["proposedCode"] for l in lines), moved)
issued = sorted(moved["archiveCodes"])
in_file = call("GET", GW + "/legal-app/cases/%d" % case, team)[1]
check("each sale carries its code in the debtor file", sorted(a["archiveCode"] for a in in_file["accounts"]) == issued, in_file["accounts"])
check("the debtor is found by an archive code", any(c["id"] == case for c in call("GET", GW + "/legal-app/cases?q=" + q(issued[0]), team)[1]))
check("the code shows on the company dashboard too",
      issued[0] in (call("GET", GW + "/legal-desk/persons/%d" % person, accountant)[1]["person"]["archiveCodes"] or ""))
call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Recent"})
status, again = call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Archive"})
check("going back and re-entering the archive keeps the same codes", status == 200 and again["archiveCodes"] == []
      and sorted(a["archiveCode"] for a in call("GET", GW + "/legal-app/cases/%d" % case, team)[1]["accounts"]) == issued, again)
before = call("GET", GW + "/legal-app/registers", team)[1]
status, refused = call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Judicial"})
check("a judicial case cannot start without a lawyer", status == 409 and refused.get("code") == "LawyerRequired", refused)
first_lawyer = call("POST", GW + "/legal-app/lawyers", team, {"name": "محامي الدعوى %d" % int(time.time())})[1]["id"]
status, moved = call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Judicial", "lawyerId": first_lawyer})
check("archive -> judicial with a lawyer charges nothing (the fee was taken on sending)", status == 200 and moved["charged"] == 0, moved)
check("the lawyer chosen in the move is assigned",
      call("GET", GW + "/legal-app/cases/%d" % case, team)[1]["legalCase"]["lawyerId"] == first_lawyer)
check("the lawyer cannot be removed once the case started",
      call("POST", GW + "/legal-app/cases/%d/lawyer" % case, team, {"lawyerId": None})[0] == 409)
after = call("GET", GW + "/legal-app/registers", team)[1]
check("دين قلعة الضمان is unchanged by starting the judicial case", after["companyDebt"] == before["companyDebt"], (before["companyDebt"], after["companyDebt"]))
step_types = call("GET", GW + "/legal-app/step-types", team)[1]
judicial_step = next(s for s in step_types if s["flow"] == "Judicial")
for _ in range(2):
    status, _step = call("POST", GW + "/legal-app/cases/%d/steps" % case, team, {"stepTypeId": judicial_step["id"], "note": "smoke"})
check("same step can be logged twice", status == 200)
execution_step = next(s for s in step_types if s["flow"] == "Execution")
check("execution step refused while in judicial",
      call("POST", GW + "/legal-app/cases/%d/steps" % case, team, {"stepTypeId": execution_step["id"]})[0] == 400)
status, new_type = call("POST", GW + "/legal-app/step-types", team, {"flow": "Judicial", "name": "خطوة تجريبية %d" % int(time.time())})
check("user can add a step type", status == 200, new_type)

print("5) lawyers, wallet, fees, registers")
status, lawyer = call("POST", GW + "/legal-app/lawyers", team, {"name": "محامي المختبر %d" % int(time.time())})
status2, other = call("POST", GW + "/legal-app/lawyers", team, {"name": "محامي آخر %d" % int(time.time())})
lawyer, other = lawyer["id"], other["id"]
check("assign lawyer", call("POST", GW + "/legal-app/cases/%d/lawyer" % case, team, {"lawyerId": lawyer})[0] == 200)
cash_before = call("GET", GW + "/legal-app/registers", team)[1]
check("company funds add to both registers", call("POST", GW + "/legal-app/registers/company-funds", team, {"amount": 1000000, "note": "smoke"})[0] == 200)
regs = call("GET", GW + "/legal-app/registers", team)[1]
check("cash +1,000,000 and debt +1,000,000", regs["cash"] == cash_before["cash"] + 1000000 and regs["companyDebt"] == cash_before["companyDebt"] + 1000000, regs)
check("transfer to lawyer wallet", call("POST", GW + "/legal-app/lawyers/%d/transfer" % lawyer, team, {"amount": 300000})[0] == 200)
png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=")
status, fee = call("POST", GW + "/legal-app/lawyers/%d/fees" % lawyer, team,
                   form={"caseId": case, "amount": 50000, "note": "رسم دعوى", "outsideAssignment": "false", "image": ("r.png", png, "image/png")})
check("fee with picture from assigned lawyer", status == 200, fee)
check("fee picture can be read back", call("GET", GW + "/legal-app/fees/%d/image" % fee["id"], team)[0] == 200)
status, refused = call("POST", GW + "/legal-app/lawyers/%d/fees" % other, team, form={"caseId": case, "amount": 10000, "note": "x", "outsideAssignment": "false"})
check("unassigned lawyer needs the explicit option", status == 409 and refused.get("code") == "NotAssigned", refused)
check("unassigned lawyer with the option works",
      call("POST", GW + "/legal-app/lawyers/%d/fees" % other, team, form={"caseId": case, "amount": 10000, "note": "x", "outsideAssignment": "true"})[0] == 200)
profile = call("GET", GW + "/legal-app/lawyers/%d" % lawyer, team)[1]
check("lawyer wallet = 300,000 - 50,000", profile["lawyer"]["balance"] == 250000 and len(profile["cases"]) == 1, profile["lawyer"])
status, sub_step = call("POST", GW + "/legal-app/cases/%d/steps" % case, team, {"stepTypeId": judicial_step["id"], "lawyerId": other, "note": "بديل"})
status2, own_step = call("POST", GW + "/legal-app/cases/%d/steps" % case, team, {"stepTypeId": judicial_step["id"]})
after_steps = call("GET", GW + "/legal-app/cases/%d" % case, team)[1]
by_id = {s["id"]: s for s in after_steps["extras"]["steps"]}
check("a step can be given to another lawyer for that step only",
      status == 200 and by_id[sub_step["id"]]["lawyerId"] == other and by_id[sub_step["id"]]["isSubstitute"] is True, by_id.get(sub_step["id"]))
check("the debtor stays with his assigned lawyer", after_steps["legalCase"]["lawyerId"] == lawyer, after_steps["legalCase"]["lawyerId"])
check("the next step defaults back to the assigned lawyer",
      status2 == 200 and by_id[own_step["id"]]["lawyerId"] == lawyer and by_id[own_step["id"]]["isSubstitute"] is False, by_id.get(own_step["id"]))
check("the substitute lawyer's profile lists that step",
      any(s["caseId"] == case for s in call("GET", GW + "/legal-app/lawyers/%d" % other, team)[1]["substituteSteps"]))
import datetime, subprocess
future = (datetime.date.today() + datetime.timedelta(days=5)).isoformat()
status, planned = call("POST", GW + "/legal-app/cases/%d/steps" % case, team, {"stepTypeId": judicial_step["id"], "date": future, "note": "موعد"})
upcoming = call("GET", GW + "/legal-app/upcoming-steps", team)[1]
mine = next((u for u in upcoming if u["id"] == planned["id"]), None)
check("a step with a future date shows under upcoming steps", status == 200 and mine is not None and mine["daysLeft"] > 0 and mine["caseId"] == case, mine)
check("it shows on the debtor and on his lawyer", any(u["id"] == planned["id"] for u in call("GET", GW + "/legal-app/upcoming-steps?caseId=%d" % case, team)[1])
      and any(u["id"] == planned["id"] for u in call("GET", GW + "/legal-app/upcoming-steps?lawyerId=%d" % lawyer, team)[1])
      and not any(u["id"] == planned["id"] for u in call("GET", GW + "/legal-app/upcoming-steps?lawyerId=%d" % other, team)[1]))
check("a step dated today is done, not upcoming", not any(u["id"] == own_step["id"] for u in upcoming))
subprocess.run(["sqlcmd", "-S", ".\\SQLEXPRESS", "-E", "-C", "-d", "LegalLab_Legal", "-Q",
                "UPDATE dbo.LegalCaseSteps SET StepDate = DATEADD(DAY, -8, StepDate) WHERE Id = %d" % planned["id"]], check=True, capture_output=True)
late = next(u for u in call("GET", GW + "/legal-app/upcoming-steps", team)[1] if u["id"] == planned["id"])
check("once its date passes it stays listed as overdue", late["daysLeft"] < 0, late)
check("marking it done removes it from upcoming", call("POST", GW + "/legal-app/cases/%d/steps/%d/done" % (case, planned["id"]), team, {"done": True})[0] == 200
      and not any(u["id"] == planned["id"] for u in call("GET", GW + "/legal-app/upcoming-steps", team)[1]))
check("expense needs a reason", call("POST", GW + "/legal-app/registers/expenses", team, {"amount": 5000})[0] == 400)
check("expense leaves the cash box", call("POST", GW + "/legal-app/registers/expenses", team, {"amount": 5000, "note": "قرطاسية"})[0] == 200)
cash_now = call("GET", GW + "/legal-app/registers", team)[1]["cash"]
check("the cash box may go negative", call("POST", GW + "/legal-app/registers/expenses", team, {"amount": cash_now + 70000, "note": "تجربة السالب"})[0] == 200
      and call("GET", GW + "/legal-app/registers", team)[1]["cash"] == -70000)
check("bring the cash box back", call("POST", GW + "/legal-app/registers/company-funds", team, {"amount": cash_now + 70000, "note": "smoke"})[0] == 200)
case_detail = call("GET", GW + "/legal-app/cases/%d" % case, team)[1]
check("debtor profile lists both fees (60,000)", sum(f["amount"] for f in case_detail["extras"]["fees"]) == 60000, case_detail["extras"]["fees"])
check("company dashboard reads registers", call("GET", GW + "/legal-desk/registers", accountant)[0] == 200)
check("company dashboard cannot change registers", call("POST", GW + "/legal-app/registers/expenses", accountant, {"amount": 1, "note": "x"})[0] == 403)

print("6) payments, receipts and discounts")
check("ابطال and the full-settlement request are gone",
      call("POST", GW + "/legal-app/cases/%d/annul" % case, team, {"annulled": True, "amount": 1000})[0] in (404, 405)
      and call("POST", GW + "/legal-app/cases/%d/settlement-request" % case, team, {})[0] in (404, 405)
      and call("GET", GW + "/legal-desk/annul-requests", accountant)[0] in (404, 405)
      and call("GET", GW + "/legal-desk/settlement-requests", accountant)[0] in (404, 405))
status, old_receipts = call("GET", GW + "/legal-desk/old-receipts", accountant)
old_numbers = {r["receiptNumber"] for r in old_receipts} if status == 200 else set()
check("the old system's receipts are loaded and searchable",
      status == 200 and len(old_receipts) == 36
      and [r["receiptNumber"] for r in call("GET", GW + "/legal-desk/old-receipts?q=1025185214", accountant)[1]] == [1025185214]
      and call("GET", GW + "/legal-desk/old-receipts", team)[0] == 403, (status, len(old_receipts or [])))
detail = call("GET", GW + "/legal-desk/persons/%d" % person, accountant)[1]
najaf_account = next(a for a in detail["accounts"] if a["customerId"] == first["customerId"])
check("penalty of 200,000 shows on the debtor", detail["totals"]["penaltiesDue"] == 200000, detail["totals"])
branch_before = call("GET", NAJAF + "/legal-branch/customers/%d/detail" % first["customerId"],
                     headers={"X-Sales-Gateway-Key": SECRETS["GatewayKey"], "X-Legal-Viewer": "1"})[1]
status, paid = call("POST", GW + "/legal-desk/persons/%d/payments" % person, accountant,
                    {"lines": [{"accountId": najaf_account["id"], "amount": 25000}], "penaltyAmount": 100000, "note": "smoke"})
check("payment recorded with its penalty part", status == 200 and paid["ok"] and paid["debtPaid"] == 25000 and paid["penaltyTaken"] == 100000, paid)
receipt_no = paid.get("receiptNumber") or 0
check("the تسديد got a random 10-digit receipt number that the old system never used",
      1000000000 <= receipt_no <= 9999999999 and receipt_no not in old_numbers, receipt_no)
status, pdf = call("GET", GW + "/legal-desk/payments/%d/receipt" % paid["paymentId"], accountant)
check("its receipt downloads as a PDF", status == 200 and isinstance(pdf, bytes) and pdf[:4] == b"%PDF", (status, str(pdf)[:60]))
if isinstance(pdf, bytes):
    open(os.path.join(HERE, "data", "last-receipt.pdf"), "wb").write(pdf)
check("the legal team cannot download a receipt", call("GET", GW + "/legal-desk/payments/%d/receipt" % paid["paymentId"], team)[0] == 403)
detail2 = call("GET", GW + "/legal-desk/persons/%d" % person, accountant)[1]
najaf_after = next(a for a in detail2["accounts"] if a["customerId"] == first["customerId"])
check("the debtor's remaining dropped by the payment", najaf_after["remaining"] == najaf_account["remaining"] - 25000, (najaf_account["remaining"], najaf_after["remaining"]))
branch_after = call("GET", NAJAF + "/legal-branch/customers/%d/detail" % first["customerId"],
                    headers={"X-Sales-Gateway-Key": SECRETS["GatewayKey"], "X-Legal-Viewer": "1"})[1]
check("nothing was posted to the branch (payment lives in القانونية)",
      branch_after["customer"]["remaining"] == branch_before["customer"]["remaining"]
      and len(branch_after["payments"]) == len(branch_before["payments"]), (branch_before["customer"], branch_after["customer"]))
check("penalties due now 100,000", detail2["totals"]["penaltiesDue"] == 100000, detail2["totals"])
check("overpaying an account is refused",
      call("POST", GW + "/legal-desk/persons/%d/payments" % person, accountant, {"lines": [{"accountId": najaf_account["id"], "amount": 10 ** 10}], "penaltyAmount": 0})[0] == 409)
check("overpaying penalties is refused", call("POST", GW + "/legal-desk/persons/%d/payments" % person, accountant, {"lines": [], "penaltyAmount": 10 ** 9})[0] == 409)
status, collected = call("GET", GW + "/legal-desk/payments", accountant)
check("the payment shows in سجل التسديدات with its receipt number",
      status == 200 and collected[0]["personId"] == person and collected[0]["debtAmount"] == 25000
      and collected[0]["receiptNumber"] == receipt_no and collected[0]["kind"] == "Payment", collected[:1])
check("it can be found by receipt number",
      [p["id"] for p in call("GET", GW + "/legal-desk/payments?q=%d" % receipt_no, accountant)[1]] == [paid["paymentId"]])
second_pay = call("POST", GW + "/legal-desk/persons/%d/payments" % person, accountant,
                  {"lines": [{"accountId": najaf_account["id"], "amount": 1000}], "penaltyAmount": 0})[1]
check("every تسديد gets its own receipt number", second_pay["receiptNumber"] not in (receipt_no, None), second_pay)

box_before = call("GET", GW + "/legal-desk/cashbox", accountant)[1]
due_before = call("GET", GW + "/legal-desk/persons/%d" % person, accountant)[1]["totals"]
check("a discount larger than everything owed is refused",
      call("POST", GW + "/legal-desk/persons/%d/discounts" % person, accountant, {"amount": due_before["totalDue"] + 1})[0] == 409)
status, disc = call("POST", GW + "/legal-desk/persons/%d/discounts" % person, accountant, {"amount": 30000, "note": "خصم تجريبي"})
due_after = call("GET", GW + "/legal-desk/persons/%d" % person, accountant)[1]["totals"]
check("a discount lowers what is owed (debt first) and has no receipt",
      status == 200 and disc["kind"] == "Discount" and disc["receiptNumber"] is None
      and due_after["remaining"] == due_before["remaining"] - 30000
      and due_after["totalDue"] == due_before["totalDue"] - 30000, (status, disc))
check("there is no receipt PDF for a discount",
      call("GET", GW + "/legal-desk/payments/%d/receipt" % disc["paymentId"], accountant)[0] == 404)
status, disc2 = call("POST", GW + "/legal-desk/persons/%d/discounts" % person, accountant, {"amount": 20000})
check("a second discount is accepted", status == 200 and
      call("GET", GW + "/legal-desk/persons/%d" % person, accountant)[1]["totals"]["totalDue"] == due_before["totalDue"] - 50000, disc2)
check("the legal team cannot give a discount",
      call("POST", GW + "/legal-desk/persons/%d/discounts" % person, team, {"amount": 1000})[0] == 403)
disc_list = call("GET", GW + "/legal-desk/payments?kind=Discount", accountant)[1]
check("discounts have their own record and stay out of سجل التسديدات",
      [d["id"] for d in disc_list[:2]] == [disc2["paymentId"], disc["paymentId"]]
      and not any(p["kind"] != "Payment" for p in call("GET", GW + "/legal-desk/payments", accountant)[1]), disc_list[:2])
status, box = call("GET", GW + "/legal-desk/cashbox", accountant)
box_c = box["collected"] if status == 200 else {}
check("the dashboard القاصة: discounts are counted apart from the money collected",
      status == 200 and box_c["total"] == box_before["collected"]["total"]
      and box_c["total"] == box_c["debt"] + box_c["penalties"] == sum(p["debtAmount"] + p["penaltyAmount"] for p in box["payments"])
      and box["discounted"]["total"] == box_before["discounted"]["total"] + 50000
      and box["discounted"]["count"] == box_before["discounted"]["count"] + 2
      and box["discounted"]["total"] == sum(p["debtAmount"] + p["penaltyAmount"] for p in box["discounts"])
      and box["funds"]["total"] == sum(f["amount"] for f in box["funds"]["records"]) and box["funds"]["count"] == len(box["funds"]["records"])
      and box["companyDebt"] == box["funds"]["total"] - box["paidBack"]["total"], (box_c, box.get("discounted")))
check("the legal team cannot open the dashboard القاصة", call("GET", GW + "/legal-desk/cashbox", team)[0] == 403)
status, temp_type = call("POST", GW + "/legal-app/step-types", team, {"flow": "Judicial", "name": "خطوة مؤقتة للفحص", "isMandatory": True})
status_logged, _ = call("POST", GW + "/legal-app/cases/%d/steps" % case, team, {"stepTypeId": temp_type["id"]})
status_del, _ = call("DELETE", GW + "/legal-app/step-types/%d" % temp_type["id"], team)
steps_now = call("GET", GW + "/legal-app/cases/%d" % case, team)[1]["extras"]["steps"]
check("a step can be removed from the list; the one already logged stays on the file",
      status == 200 and status_logged == 200 and status_del == 200
      and not any(t["id"] == temp_type["id"] for t in call("GET", GW + "/legal-app/step-types", team)[1])
      and any(s["stepName"] == "خطوة مؤقتة للفحص" for s in steps_now), (status, status_logged, status_del))
check("removing a missing step answers 404", call("DELETE", GW + "/legal-app/step-types/%d" % temp_type["id"], team)[0] == 404)

print("7) decisions and execution")
must = {"id": new_type["id"], "flow": "Judicial", "name": "خطوة إلزامية %d" % int(time.time()), "sortOrder": 99, "isActive": True, "isMandatory": True}
check("a step can be made mandatory", call("POST", GW + "/legal-app/step-types", team, must)[0] == 200
      and any(s["id"] == must["id"] and s["isMandatory"] for s in call("GET", GW + "/legal-app/step-types", team)[1]))
status, refused = call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Decisions"})
check("cannot leave the judicial case while a mandatory step is missing", status == 409 and refused.get("code") == "MandatorySteps", refused)
status, scheduled = call("POST", GW + "/legal-app/cases/%d/steps" % case, team, {"stepTypeId": must["id"], "date": future})
check("a scheduled (not yet done) mandatory step does not unlock the move",
      call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Decisions"})[0] == 409)
check("log the mandatory step", call("POST", GW + "/legal-app/cases/%d/steps/%d/done" % (case, scheduled["id"]), team, {"done": True})[0] == 200)


def log_mandatory_judicial():
    """Logs every mandatory judicial step (the lab may hold some made by hand)."""
    for t in call("GET", GW + "/legal-app/step-types", team)[1]:
        if t["flow"] == "Judicial" and t["isActive"] and t["isMandatory"]:
            call("POST", GW + "/legal-app/cases/%d/steps" % case, team, {"stepTypeId": t["id"]})


log_mandatory_judicial()
check("judicial -> decisions", call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Decisions"})[0] == 200)
check("make the step optional again", call("POST", GW + "/legal-app/step-types", team, dict(must, isMandatory=False, isActive=False))[0] == 200)
check("court decision saved", call("POST", GW + "/legal-app/cases/%d/decision" % case, team, {"text": "قرار تجريبي", "date": "2026-11-18"})[0] == 200)
status, moved = call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Execution"})
check("decisions -> execution charges 150,000", status == 200 and moved["charged"] == 150000, moved)
check("execution step accepted now", call("POST", GW + "/legal-app/cases/%d/steps" % case, team, {"stepTypeId": execution_step["id"]})[0] == 200)
detail3 = call("GET", GW + "/legal-desk/persons/%d" % person, accountant)[1]
check("penalties due = 100,000 + 150,000", detail3["totals"]["penaltiesDue"] == 250000, detail3["totals"])
status, back = call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Archive", "note": "رجوع"})
check("a debtor can be returned from التنفيذ straight to an earlier station", status == 200 and back["to"] == "Archive" and back["charged"] == 0, back)
check("still cannot skip stations going forward", call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Decisions"})[0] == 409)
forward = [call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": "Judicial"})]
log_mandatory_judicial()
forward += [call("POST", GW + "/legal-app/cases/%d/move" % case, team, {"to": to}) for to in ("Decisions", "Execution")]
check("moving forward again charges no fee twice", all(s == 200 and m["charged"] == 0 for s, m in forward), forward)
check("penalties unchanged after going back and forward",
      call("GET", GW + "/legal-desk/persons/%d" % person, accountant)[1]["totals"]["penaltiesDue"] == 250000)
print("8) paying everything at any time, with no request")
status, created = call("POST", GW + "/legal-desk/persons", accountant,
                       {"fullName": second["customerName"], "accounts": [{"cityValue": "lab-najaf", "customerId": second["customerId"]}]})
person2 = created["id"]
before_second = call("GET", GW + "/legal-app/registers", team)[1]
case2 = call("POST", GW + "/legal-desk/persons/%d/send" % person2, accountant, {})[1]["caseId"]
after_second = call("GET", GW + "/legal-app/registers", team)[1]
d = call("GET", GW + "/legal-desk/persons/%d" % person2, accountant)[1]
check("no 35,000: a debtor still in «الاسماء المضافة مؤخرا» owes his debt + 200,000 only",
      d["totals"]["penaltiesDue"] == 200000 and after_second["earlyFees"] == before_second["earlyFees"]
      and after_second["companyDebt"] == before_second["companyDebt"] - 200000, d["totals"])
lines = [{"accountId": a["id"], "amount": a["remaining"]} for a in d["accounts"] if a["remaining"] > 0]
status, paid = call("POST", GW + "/legal-desk/persons/%d/payments" % person2, accountant, {"lines": lines, "penaltyAmount": 200000})
check("he pays everything straight away and the file closes",
      status == 200 and paid["ok"] and paid["closed"] and paid["remaining"] == 0 and paid["receiptNumber"], paid)
check("a closed file takes no more money",
      call("POST", GW + "/legal-desk/persons/%d/discounts" % person2, accountant, {"amount": 1})[0] == 409)

print("9) who can see legal customers on a branch")
list_id = big["delegateId"]
url_list = NAJAF + "/Customers/Customers_GetAll/%d&&null&&%s" % (list_id, q("الجميع"))
url_legal_tab = NAJAF + "/Customers/Customers_GetAll/0&&null&&%s" % q("القانونية")
branch_token = branch_jwt("محاسب رئيسي")
status, rows = call("GET", url_list, branch_token)
check("branch accountant sees the list without its legal customers",
      status == 200 and isinstance(rows, list) and not any(r.get("isLegal") for r in rows)
      and not any(r.get("customerID") == first["customerId"] for r in rows), (status, str(rows)[:200]))
status, legal_tab = call("GET", url_legal_tab, branch_token)
check("the old القانونية tab is empty for the branch accountant", status == 200 and legal_tab == [], (status, str(legal_tab)[:200]))
key_only = {"X-Sales-Gateway-Key": SECRETS["GatewayKey"]}
status, rows = call("GET", url_legal_tab, headers=key_only)
check("other central roles (gateway without legal stamp) see none", status == 200 and rows == [], (status, str(rows)[:200]))
status, rows = call("GET", url_legal_tab, headers=dict(key_only, **{"X-Legal-Viewer": "1"}))
check("legal viewers see the legal customers", status == 200 and len(rows) > 5, (status, str(rows)[:200]))
check("a branch login cannot fake the legal stamp",
      call("GET", url_legal_tab, branch_token, headers={"X-Legal-Viewer": "1"})[1] == [])
check("branch login cannot call the legal branch API", call("GET", NAJAF + "/legal-branch/lists", branch_token)[0] == 403)

print("10) stalled debtors (المدير only)")
idle_person = call("POST", GW + "/legal-desk/persons", accountant,
                   {"fullName": third["customerName"], "accounts": [{"cityValue": "lab-najaf", "customerId": third["customerId"]}]})[1]["id"]
idle_case = call("POST", GW + "/legal-desk/persons/%d/send" % idle_person, accountant, {})[1]["caseId"]
status, stalled0 = call("GET", GW + "/legal-desk/stalled?days=0", director)
check("the director sees open debtors at 0 days, but not closed ones or those in التنفيذ",
      status == 200 and any(r["caseId"] == idle_case for r in stalled0)
      and not any(r["caseId"] in (case, case2) for r in stalled0)
      and not any(r["stage"] in ("Execution", "Closed") for r in stalled0), (status, len(stalled0 or [])))
check("nobody is stalled for 10,000 days", call("GET", GW + "/legal-desk/stalled?days=10000", director)[1] == [])
check("the legal accountant cannot open it", call("GET", GW + "/legal-desk/stalled?days=0", accountant)[0] == 403)
subprocess.run(["sqlcmd", "-S", ".\SQLEXPRESS", "-E", "-C", "-d", "LegalLab_Legal", "-Q",
                "UPDATE dbo.LegalCases SET SentAtUtc = DATEADD(DAY, -40, SentAtUtc), StageEnteredAtUtc = DATEADD(DAY, -40, StageEnteredAtUtc) WHERE Id = %d" % idle_case],
               check=True, capture_output=True)
idle = next((r for r in call("GET", GW + "/legal-desk/stalled?days=30", director)[1] if r["caseId"] == idle_case), None)
check("a debtor idle for 40 days shows at 30 days", idle is not None and idle["idleDays"] >= 39, idle)
call("POST", GW + "/legal-app/cases/%d/move" % idle_case, team, {"to": "Archive"})
check("moving him to a new station takes him off the stalled list",
      not any(r["caseId"] == idle_case for r in call("GET", GW + "/legal-desk/stalled?days=30", director)[1]))

print("11) overview")
status, overview = call("GET", GW + "/legal-desk/overview", accountant)
check("overview totals", status == 200 and overview["money"]["judicialStarted"] >= 1 and overview["money"]["executionStarted"] >= 1, overview)
reg = overview["registers"]
check("received - paid back = what القاصة still owes قلعة الضمان",
      reg["companyFunds"] - reg["paidBack"] == reg["companyDebt"]
      and reg["paidBack"] == reg["judicialFees"] + reg["executionFees"] + reg["earlyFees"], reg)
print("   القاصة: cash=%s wallets=%s received=%s paid-back=%s left=%s" % (
    reg["cash"], reg["lawyerWallets"], reg["companyFunds"], reg["paidBack"], reg["companyDebt"]))

print("\n%d failed" % len(failures) if failures else "\nALL PASSED")
sys.exit(1 if failures else 0)
