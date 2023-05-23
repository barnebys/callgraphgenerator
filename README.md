# Call Graph Generator

## Usage

`callgraphgenerator projectfile namespace classname methodname [--output outputfile.puml]`


### Example
The following example will run the call graph generator for the SkeletonApi.sln solution, 
for the method `ApproveOrderAndCreateInvoice()` in the `Skeleton.Legacy.Service.InvoiceService` class.
It will save the output in the `callgraph.puml` file instead of outputing it to the standard output.

```/Users/matsfredriksson/Documents/skeleton/skeleton-api/SkeletonApi.sln
Skeleton.Legacy.Service InvoiceService ApproveOrderAndCreateInvoice --output callgraph.puml
    -i QueryFilterBuilder -i Clock -i SettingService -i IAuthorizedUserService
    -i SystemSettingsService -i StreamExtensions -i EnumerableExtensions
    -i IOrderContext -i InvoiceDataService -i KeyValueSettingService
    -i MarginVatService -i InvoiceGeneratorFactory -i WinnerTrackingService
    -i CompanyService -i IInvoiceGenerator -i CustomerService
    -i InventoryDocumentService -i PaymentContext -i GenericRepository
    -l EAccountingInvoicingService -l InvoiceSettingService -l InventoryDocumentService
    -l SmailService -l EUTaxCalculator -l AuctionService
    -l AuctionPaymentService -l InventoryItemService```
