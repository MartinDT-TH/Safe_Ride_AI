# Risk Protection Capstone Demo Checklist

Use seeded/demo accounts and a disposable authorized demo database. Do not apply
migrations or create opening-balance/adjustment records against an unapproved
database.

## Before the demo

- [ ] API, Flutter app, and React management app use the intended demo environment.
- [ ] Customer, Driver, Staff, and Admin accounts can sign in.
- [ ] The selected vehicle belongs to the Customer.
- [ ] If demonstrating external vehicle insurance, the vehicle has a Staff-verified,
      effective `PHYSICAL_DAMAGE` policy. Do not use `MANDATORY_TPL` as own-vehicle
      coverage.
- [ ] Risk Fund has an authorized positive demo balance.
- [ ] Choose one of the six scenarios in
      [the Risk Protection guide](risk-protection-guide.md#six-verified-demo-scenarios).
- [ ] Prepare genuine demo evidence and payment references. Never describe Mock
      Insurance as a real insurer connection.

## Live walkthrough

1. [ ] Admin opens **Risk Fund** → **Cài đặt chính sách** and shows the current
       effective version plus immutable version history.
2. [ ] Admin shows **Tổng quan** and the current Risk Fund balance.
3. [ ] Customer opens Profile → My Vehicles → **Bảo hiểm/Insurance**, shows the
       vehicle and localized verification status, and explains `PHYSICAL_DAMAGE`
       versus `MANDATORY_TPL`.
4. [ ] Create/assign the demo trip and let the Driver arrive.
5. [ ] Driver completes **Kiểm tra an toàn trước chuyến / Pre-trip vehicle safety
       check**. Explain that it is a reasonable visible check, not a mechanical
       inspection.
6. [ ] Start the trip.
7. [ ] Verify the trip has exactly one Risk Protection coverage snapshot through
       the authorized API/database inspection used by the demo environment.
8. [ ] Customer or Driver chooses **Báo cáo tai nạn / Report accident**, enters a
       factual description, and creates the case.
9. [ ] On the accident case, choose **Thêm ảnh bằng chứng / Add evidence photo**.
       Capture with the rear camera and submit the note. Do not substitute a
       Gallery-only demonstration.
10. [ ] Staff opens **Tai nạn & Trách nhiệm** → **Mở hồ sơ** and reviews step 1,
        **Bằng chứng & thông tin sự cố**.
11. [ ] Staff completes step 2, **Nguyên nhân**, and step 3, **Phân bổ trách nhiệm**.
        Show that responsibility and cause totals equal 100 percent, then choose
        **Xác nhận trách nhiệm**.
12. [ ] Staff completes step 4 and chooses **Yêu cầu máy chủ tính đề xuất**. Show
        the adjacent **Đề xuất từ máy chủ** and explain that React did not calculate it.
13. [ ] In step 5, **Rà soát & thực hiện**, review the claim summary. There is no
        separate invented “Confirm claim” button: the authoritative actions are
        responsibility confirmation, server calculation, and permitted funding.
14. [ ] If the scenario uses vehicle insurance, open **Thao tác kế toán nâng cao
        & kiểm toán**, demonstrate the Mock Insurance approval/rejection, and state
        explicitly that it is a capstone simulation.
15. [ ] Choose **Cấp kinh phí / thử lại cấp kinh phí**, read the impact confirmation,
        confirm once, and show the resulting claim status.
16. [ ] If the scenario is recoverable, record the actual Driver, Customer, Third
        Party, or Insurance recovery with evidence and a payment reference. Show
        that retry/double-submit does not create a second credit.
17. [ ] When exposure is reconciled, choose **Đóng hồ sơ đã đối soát**. If the
        server rejects closure, identify and resolve the outstanding material item;
        do not bypass it.
18. [ ] Admin returns to **Risk Fund** → **Sổ cái**, filters as needed, and shows
        contribution, advance/payout, recovery, balances before/after, and linked
        Trip/Claim references.

## Evidence to capture for the presentation

- [ ] Current policy version and effective time.
- [ ] Coverage snapshot ID for the demo trip.
- [ ] Accident case ID and evidence timestamp.
- [ ] Confirmed five-bucket responsibility summary.
- [ ] Server recommendation breakdown.
- [ ] Claim status before and after funding.
- [ ] Relevant Risk Fund ledger debit and recovery credit.
- [ ] Final reconciled/closed state, or an intentional insufficient-funds state
      showing no negative/partial debit.
- [ ] DriverWallet before and after for a liability scenario, proving there was no
      automatic deduction.

## Required verbal disclaimers

- [ ] SafeRide Vehicle Protection has no Basic/Standard/Premium tiers.
- [ ] Contributions come from platform-side economics, not hidden Driver deductions.
- [ ] Fault percentage and payment percentage are different concepts.
- [ ] Customer intoxication alone is not fault.
- [ ] Risk Fund advances and permanent losses are distinct.
- [ ] Mock Insurance Provider is a simulation. SafeRide is not presented as a
      licensed insurer, no real insurer API is connected, and the simulation is
      not a legal insurance policy.
