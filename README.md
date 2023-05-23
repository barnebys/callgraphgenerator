# Call Graph Generator

## Usage
```
PlantUml Call Grapher
Description:
PlantUml Call Grapher

Usage:
CallGraphGenerator <file> <namespace> <class> <method> [options]

Arguments:
<file>       The project or solution file to process.
<namespace>  Namespace of entry point
<class>      Class of entry point
<method>     Method entry point

Options:
-o, --output <output>  Optional output file
-l, --leaf <leaf>      Classes that should not be parsed further.
-i, --ignore <ignore>  Classes that should be completely ignored.
--version              Show version information
-?, -h, --help         Show help and usage information
```

### Example
The following example will run the call graph generator for the SkeletonApi.sln solution, 
for the method `ApproveOrderAndCreateInvoice()` in the `Skeleton.Legacy.Service.InvoiceService` class.
It will save the output in the `callgraph.puml` file instead of outputing it to the standard output.

```sh
$ CallGraphGenerator /Users/matsfredriksson/Documents/skeleton/skeleton-api/SkeletonApi.sln \
    Skeleton.Legacy.Service InvoiceService ApproveOrderAndCreateInvoice --output callgraph.puml \
    -i QueryFilterBuilder -i Clock -i SettingService -i IAuthorizedUserService \
    -i SystemSettingsService -i StreamExtensions -i EnumerableExtensions \
    -i IOrderContext -i InvoiceDataService -i KeyValueSettingService \
    -i MarginVatService -i InvoiceGeneratorFactory -i WinnerTrackingService \
    -i CompanyService -i IInvoiceGenerator -i CustomerService \
    -i InventoryDocumentService -i PaymentContext -i GenericRepository \
    -l EAccountingInvoicingService -l InvoiceSettingService -l InventoryDocumentService \
    -l SmailService -l EUTaxCalculator -l AuctionService \
    -l AuctionPaymentService -l InventoryItemService```
